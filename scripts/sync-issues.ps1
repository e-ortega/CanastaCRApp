<#
.SYNOPSIS
  Syncs unchecked Phase 2 / Phase 3 items from CLAUDE.md to GitHub Issues.
  Skips items that already have a matching open issue (by title).
  Safe to run multiple times — will not create duplicates.
.EXAMPLE
  .\scripts\sync-issues.ps1
  .\scripts\sync-issues.ps1 -DryRun
#>
param([switch]$DryRun)

$Repo   = 'e-ortega/CanastaCRApp'
$Claude = Join-Path $PSScriptRoot '..\CLAUDE.md'

# ── Ensure labels exist ───────────────────────────────────────────────────────
$labels = @(
    @{ name = 'phase-2'; color = '0075ca'; description = 'Phase 2 — Mobile + richer features' }
    @{ name = 'phase-3'; color = 'e4e669'; description = 'Phase 3 — Automation + Intelligence' }
    @{ name = 'enhancement'; color = 'a2eeef'; description = '' }
)

foreach ($label in $labels) {
    $existing = gh label list --repo $Repo --json name | ConvertFrom-Json | Where-Object { $_.name -eq $label.name }
    if (-not $existing) {
        if ($DryRun) {
            Write-Host "[dry-run] create label: $($label.name)" -ForegroundColor DarkGray
        } else {
            gh label create $label.name --color $label.color --description $label.description --repo $Repo 2>$null
        }
    }
}

# ── Parse CLAUDE.md for unchecked items ──────────────────────────────────────
$content     = Get-Content $Claude -Raw
$currentPhase = $null
$items        = @()

foreach ($line in (Get-Content $Claude)) {
    if ($line -match '^### Phase (\d+)') {
        $currentPhase = $Matches[1]
    }
    # Unchecked checkbox: "- [ ] Some task"
    if ($line -match '^\s*- \[ \] (.+)$' -and $currentPhase -in @('2', '3')) {
        $items += @{ phase = $currentPhase; title = $Matches[1].Trim() }
    }
}

Write-Host "Found $($items.Count) open items across Phase 2 and 3" -ForegroundColor Cyan

# ── Fetch existing open issues to avoid duplicates ────────────────────────────
$existing = gh issue list --repo $Repo --state open --limit 200 --json title | ConvertFrom-Json | ForEach-Object { $_.title }

# ── Create missing issues ─────────────────────────────────────────────────────
$created = 0
foreach ($item in $items) {
    if ($existing -contains $item.title) {
        Write-Host "  skip (exists): $($item.title)" -ForegroundColor DarkGray
        continue
    }

    $phaseLabel = "phase-$($item.phase)"
    if ($DryRun) {
        Write-Host "  [dry-run] create issue [$phaseLabel]: $($item.title)" -ForegroundColor Yellow
    } else {
        gh issue create `
            --repo  $Repo `
            --title $item.title `
            --label "$phaseLabel,enhancement" `
            --body  "Tracked from Phase $($item.phase) in CLAUDE.md."
        Write-Host "  ✓ created [$phaseLabel]: $($item.title)" -ForegroundColor Green
    }
    $created++
}

Write-Host ""
Write-Host "$created issue(s) created. Run again after adding new items to CLAUDE.md." -ForegroundColor White
