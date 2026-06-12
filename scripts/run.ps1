<#
.SYNOPSIS
  CanastaCR dev task runner.
.EXAMPLE
  .\scripts\run.ps1 api              # run API in watch mode
  .\scripts\run.ps1 api:build        # build API (Release)
  .\scripts\run.ps1 api:test         # run all backend tests
  .\scripts\run.ps1 app              # run Flutter app in Chrome
  .\scripts\run.ps1 app:build        # flutter build web (Release)
  .\scripts\run.ps1 app:test         # run Flutter unit tests
  .\scripts\run.ps1 infra:validate   # validate Bicep without deploying
  .\scripts\run.ps1 infra:deploy     # deploy Bicep to Azure (prod)
  .\scripts\run.ps1 help             # show this list
#>
param([string]$Command = 'help')

$Root    = Split-Path $PSScriptRoot -Parent
$Flutter = 'C:\Users\eorte\source\repos\flutter\bin\flutter'
$ApiSrc  = Join-Path $Root 'api\src\CanastaCR.Api'
$ApiSln  = Join-Path $Root 'api'
$AppDir  = Join-Path $Root 'app'
$InfraDir= Join-Path $Root 'infra'

function Require-EnvVar([string]$Name) {
    if (-not (Get-Item "env:$Name" -ErrorAction SilentlyContinue)) {
        Write-Error "Missing env var: $Name — set it before running this command."
        exit 1
    }
}

switch ($Command) {

    # ── API ───────────────────────────────────────────────────────────────
    'api' {
        Write-Host "▶ Running API (https://localhost:7068/swagger)" -ForegroundColor Cyan
        dotnet run --project $ApiSrc --launch-profile https
    }

    'api:watch' {
        Write-Host "▶ Running API with hot-reload" -ForegroundColor Cyan
        dotnet watch --project $ApiSrc run --launch-profile https
    }

    'api:build' {
        Write-Host "▶ Building API (Release)" -ForegroundColor Cyan
        dotnet build $ApiSln --configuration Release
    }

    'api:test' {
        Write-Host "▶ Running backend tests" -ForegroundColor Cyan
        dotnet test "$ApiSln\tests\CanastaCR.Tests" --logger "console;verbosity=normal"
    }

    'api:publish' {
        Write-Host "▶ Publishing API to api/publish" -ForegroundColor Cyan
        dotnet publish $ApiSrc --configuration Release --output "$Root\api\publish"
    }

    # ── Flutter app ───────────────────────────────────────────────────────
    'app' {
        Write-Host "▶ Running Flutter app in Chrome" -ForegroundColor Cyan
        Push-Location $AppDir
        & $Flutter run -d chrome
        Pop-Location
    }

    'app:build' {
        Write-Host "▶ Building Flutter web (Release)" -ForegroundColor Cyan
        $ApiUrl = $env:API_URL ?? 'https://localhost:7068'
        Push-Location $AppDir
        & $Flutter build web --release --dart-define=API_URL=$ApiUrl
        Pop-Location
    }

    'app:test' {
        Write-Host "▶ Running Flutter unit tests" -ForegroundColor Cyan
        Push-Location $AppDir
        & $Flutter test test/models/
        Pop-Location
    }

    'app:analyze' {
        Write-Host "▶ Flutter analyze" -ForegroundColor Cyan
        Push-Location $AppDir
        & $Flutter analyze
        Pop-Location
    }

    # ── Infrastructure ────────────────────────────────────────────────────
    'infra:validate' {
        Write-Host "▶ Validating Bicep (no deploy)" -ForegroundColor Cyan
        Require-EnvVar 'AZURE_RESOURCE_GROUP'
        Require-EnvVar 'POSTGRES_ADMIN_PASSWORD'
        Require-EnvVar 'JWT_SECRET'
        az deployment group validate `
            --resource-group $env:AZURE_RESOURCE_GROUP `
            --template-file "$InfraDir\main.bicep" `
            --parameters "$InfraDir\main.bicepparam"
    }

    'infra:deploy' {
        Write-Host "▶ Deploying infra to Azure (prod)" -ForegroundColor Yellow
        Require-EnvVar 'AZURE_RESOURCE_GROUP'
        Require-EnvVar 'POSTGRES_ADMIN_PASSWORD'
        Require-EnvVar 'JWT_SECRET'
        $RunName = "manual-$(Get-Date -Format 'yyyyMMdd-HHmm')"
        $RgLocation = $env:AZURE_LOCATION ?? 'canadacentral'
        az group create --name $env:AZURE_RESOURCE_GROUP --location $RgLocation
        az deployment group create `
            --resource-group $env:AZURE_RESOURCE_GROUP `
            --template-file "$InfraDir\main.bicep" `
            --parameters "$InfraDir\main.bicepparam" `
            --name $RunName
    }

    'infra:outputs' {
        Write-Host "▶ Showing last deployment outputs" -ForegroundColor Cyan
        Require-EnvVar 'AZURE_RESOURCE_GROUP'
        az deployment group list `
            --resource-group $env:AZURE_RESOURCE_GROUP `
            --query "[0].properties.outputs" -o json
    }

    # ── Combined ──────────────────────────────────────────────────────────
    'test' {
        Write-Host "▶ Running all tests (backend + Flutter)" -ForegroundColor Cyan
        dotnet test "$ApiSln\tests\CanastaCR.Tests" --logger "console;verbosity=normal"
        Push-Location $AppDir
        & $Flutter test test/models/
        Pop-Location
    }

    # ── Help ──────────────────────────────────────────────────────────────
    default {
        Write-Host ""
        Write-Host "CanastaCR task runner" -ForegroundColor White
        Write-Host ""
        Write-Host "  API" -ForegroundColor Yellow
        Write-Host "    api           Run API (https://localhost:7068/swagger)"
        Write-Host "    api:watch     Run API with hot-reload"
        Write-Host "    api:build     Build Release"
        Write-Host "    api:test      Run xUnit tests (28)"
        Write-Host "    api:publish   Publish to api/publish/"
        Write-Host ""
        Write-Host "  Flutter" -ForegroundColor Yellow
        Write-Host "    app           Run in Chrome"
        Write-Host "    app:build     flutter build web --release"
        Write-Host "    app:test      Run unit tests (30)"
        Write-Host "    app:analyze   flutter analyze"
        Write-Host ""
        Write-Host "  Infrastructure" -ForegroundColor Yellow
        Write-Host "    infra:validate  Validate Bicep (no deploy)"
        Write-Host "    infra:deploy    Deploy to Azure (needs env vars)"
        Write-Host "    infra:outputs   Show last deployment outputs"
        Write-Host ""
        Write-Host "  Combined" -ForegroundColor Yellow
        Write-Host "    test          Run backend + Flutter tests"
        Write-Host ""
        Write-Host "  Env vars for infra commands:" -ForegroundColor DarkGray
        Write-Host "    `$env:AZURE_RESOURCE_GROUP   = 'canastacr-rg'"
        Write-Host "    `$env:AZURE_LOCATION         = 'canadacentral'  # optional, default: canadacentral"
        Write-Host "    `$env:POSTGRES_ADMIN_PASSWORD = '...'"
        Write-Host "    `$env:JWT_SECRET              = '...'"
        Write-Host ""
    }
}
