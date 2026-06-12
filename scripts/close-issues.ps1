<#
.SYNOPSIS
  Closes GitHub Issues for CLAUDE.md phase items that were just checked off ([ ] → [x]).
  Called automatically by .githooks/post-commit when CLAUDE.md is committed.
  Safe to run manually too.
.EXAMPLE
  .\scripts\close-issues.ps1
#>

$Repo   = 'e-ortega/CanastaCRApp'
$Commit = git rev-parse --short HEAD

# Find items that changed from "- [ ]" to "- [x]" in the last commit.
# Guard against first commit (no HEAD~1).
$parentExists = git rev-parse --verify HEAD~1 2>$null
if (-not $parentExists) { exit 0 }

$diff = git diff HEAD~1 HEAD -- CLAUDE.md 2>$null
if (-not $diff) { exit 0 }

$nowChecked = $diff `
    | Where-Object { $_ -match '^\+- \[x\] ' } `
    | ForEach-Object { ($_ -replace '^\+- \[x\] ', '').Trim() }

if (-not $nowChecked) { exit 0 }

Write-Host "Checking $($nowChecked.Count) completed item(s) against open issues..." -ForegroundColor Cyan

foreach ($title in $nowChecked) {
    $matches = gh issue list `
        --repo   $Repo `
        --state  open `
        --search "`"$title`"" `
        --json   number,title `
    | ConvertFrom-Json `
    | Where-Object { $_.title -eq $title }

    if (-not $matches) {
        Write-Host "  no open issue for: $title" -ForegroundColor DarkGray
        continue
    }

    $issue = $matches | Select-Object -First 1
    gh issue close $issue.number `
        --repo    $Repo `
        --comment "Resolved in $Commit. Marked complete in CLAUDE.md."
    Write-Host "  ✓ closed #$($issue.number): $title" -ForegroundColor Green
}
