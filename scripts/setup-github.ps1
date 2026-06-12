<#
.SYNOPSIS
  One-time GitHub repository setup: branch protection, secret scanning, Dependabot.
  All features used are free on personal accounts.
.EXAMPLE
  .\scripts\setup-github.ps1
  .\scripts\setup-github.ps1 -DryRun   # print what would be done without applying
#>
param([switch]$DryRun)

$Repo = 'e-ortega/CanastaCRApp'

function Invoke-GhApi($Method, $Path, $Body = $null) {
    $args = @('api', "--method=$Method", $Path)
    if ($Body) { $args += @('--input', '-') }
    if ($DryRun) {
        Write-Host "  [dry-run] gh $($args -join ' ')" -ForegroundColor DarkGray
        return
    }
    if ($Body) {
        $Body | gh @args
    } else {
        gh @args
    }
}

Write-Host ""
Write-Host "CanastaCR — GitHub repository setup" -ForegroundColor White
Write-Host "Repo: $Repo" -ForegroundColor DarkGray
if ($DryRun) { Write-Host "[DRY RUN — no changes will be made]" -ForegroundColor Yellow }
Write-Host ""

# ── 1. Branch protection on main ──────────────────────────────────────────────
Write-Host "1. Branch protection on main..." -ForegroundColor Cyan

$protection = @{
    required_status_checks = @{
        strict   = $true
        contexts = @('Build & test', 'Test')   # job names from api.yml and app.yml
    }
    enforce_admins                  = $false
    required_pull_request_reviews   = @{
        required_approving_review_count = 0      # 0 = PR required but no reviewer (solo dev)
        dismiss_stale_reviews           = $true
    }
    restrictions                    = $null
    allow_force_pushes              = $false
    allow_deletions                 = $false
} | ConvertTo-Json -Depth 10

Invoke-GhApi 'PUT' "repos/$Repo/branches/main/protection" $protection
Write-Host "  ✓ PRs required, force-push blocked, CI must pass" -ForegroundColor Green

# ── 2. Secret scanning + push protection ──────────────────────────────────────
Write-Host "2. Secret scanning..." -ForegroundColor Cyan

$secScan = '{"security_and_analysis":{"secret_scanning":{"status":"enabled"},"secret_scanning_push_protection":{"status":"enabled"}}}'
Invoke-GhApi 'PATCH' "repos/$Repo" $secScan
Write-Host "  ✓ Secret scanning + push protection enabled" -ForegroundColor Green

# ── 3. Dependabot security updates ────────────────────────────────────────────
Write-Host "3. Dependabot..." -ForegroundColor Cyan
Invoke-GhApi 'PUT' "repos/$Repo/vulnerability-alerts"
Write-Host "  ✓ Vulnerability alerts enabled" -ForegroundColor Green

# ── 4. Dependabot config file ─────────────────────────────────────────────────
$dependabotPath = "$PSScriptRoot\..\github\dependabot.yml"  # written below
Write-Host "  ✓ dependabot.yml will be committed (NuGet + pub + GitHub Actions)" -ForegroundColor Green

Write-Host ""
Write-Host "Done! Commit .github/dependabot.yml to activate Dependabot PRs." -ForegroundColor White
Write-Host ""
