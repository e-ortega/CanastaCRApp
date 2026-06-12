<#
.SYNOPSIS
  Checks whether a region has the quota and availability needed for CanastaCR.
.EXAMPLE
  .\scripts\check-region.ps1 canadacentral
#>
param([string]$Location = 'canadacentral')

# Normalize: remove spaces, lowercase — used to compare against provider location strings
function Normalize([string]$s) { ($s -replace '\s+', '').ToLower() }
function HasLocation([string[]]$list, [string]$loc) {
    $n = Normalize $loc
    return ($list | Where-Object { (Normalize $_) -eq $n }).Count -gt 0
}

Write-Host ""
Write-Host "Checking region: $Location" -ForegroundColor Cyan
Write-Host "─────────────────────────────────" -ForegroundColor DarkGray

# 1. PostgreSQL Flexible Server — SKU is nested: [].supportedFlexibleServerEditions[].supportedServerVersions[].supportedVcores[]
Write-Host "`n[1] PostgreSQL Flexible Server (Standard_B1ms)" -ForegroundColor Yellow
$pgJson = az postgres flexible-server list-skus --location $Location --output json 2>$null
if ($pgJson) {
    $pgSkus = $pgJson | ConvertFrom-Json
    # Check OfferRestricted feature flag first
    $offerRestricted = ($pgSkus[0].supportedFeatures | Where-Object { $_.name -eq 'OfferRestricted' }).status
    if ($offerRestricted -eq 'Enabled') {
        Write-Host "    FAIL — OfferRestricted is Enabled on this subscription for this region" -ForegroundColor Red
    } else {
        # Correct path: supportedServerEditions[].supportedServerSkus[]
        $b1ms = $pgSkus |
            ForEach-Object { $_.supportedServerEditions } |
            ForEach-Object { $_.supportedServerSkus } |
            Where-Object { $_.name -eq 'Standard_B1ms' }
        if ($b1ms) {
            Write-Host "    OK — Standard_B1ms available (OfferRestricted: Disabled)" -ForegroundColor Green
        } else {
            $available = $pgSkus |
                ForEach-Object { $_.supportedServerEditions } |
                ForEach-Object { $_.supportedServerSkus } |
                Select-Object -ExpandProperty name -Unique
            Write-Host "    WARN — Standard_B1ms not listed. Available: $($available -join ', ')" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "    FAIL — API returned no data" -ForegroundColor Red
}

# 2. Static Web Apps — available only in specific regions
Write-Host "`n[2] Static Web Apps" -ForegroundColor Yellow
$swaLocs = az provider show --namespace Microsoft.Web `
    --query "resourceTypes[?resourceType=='staticSites'].locations[]" `
    --output tsv 2>$null
if (HasLocation $swaLocs $Location) {
    Write-Host "    OK" -ForegroundColor Green
} else {
    $pretty = ($swaLocs | ForEach-Object { $_.Trim() }) -join ', '
    Write-Host "    FAIL — not available here. Supported: $pretty" -ForegroundColor Red
}

# 3. App Service Plan — use sites (Web App) locations; serverfarms list is incomplete in provider API
Write-Host "`n[3] App Service" -ForegroundColor Yellow
$aspLocs = az provider show --namespace Microsoft.Web `
    --query "resourceTypes[?resourceType=='sites'].locations[]" `
    --output tsv 2>$null
if (HasLocation $aspLocs $Location) {
    Write-Host "    OK" -ForegroundColor Green
} else {
    Write-Host "    FAIL (provider list may be incomplete — verify manually)" -ForegroundColor Yellow
}

# 4. Key Vault
Write-Host "`n[4] Key Vault" -ForegroundColor Yellow
$kvLocs = az provider show --namespace Microsoft.KeyVault `
    --query "resourceTypes[?resourceType=='vaults'].locations[]" `
    --output tsv 2>$null
if (HasLocation $kvLocs $Location) {
    Write-Host "    OK" -ForegroundColor Green
} else {
    Write-Host "    FAIL" -ForegroundColor Red
}

# 5. Application Insights
Write-Host "`n[5] Application Insights" -ForegroundColor Yellow
$aiLocs = az provider show --namespace Microsoft.Insights `
    --query "resourceTypes[?resourceType=='components'].locations[]" `
    --output tsv 2>$null
if (HasLocation $aiLocs $Location) {
    Write-Host "    OK" -ForegroundColor Green
} else {
    Write-Host "    FAIL" -ForegroundColor Red
}

# 6. Azure SQL (alternative to PostgreSQL — already confirmed working on this subscription)
Write-Host "`n[6] Azure SQL Database" -ForegroundColor Yellow
$sqlLocs = az provider show --namespace Microsoft.Sql `
    --query "resourceTypes[?resourceType=='servers'].locations[]" `
    --output tsv 2>$null
if (HasLocation $sqlLocs $Location) {
    Write-Host "    OK" -ForegroundColor Green
} else {
    Write-Host "    FAIL" -ForegroundColor Red
}

Write-Host ""
