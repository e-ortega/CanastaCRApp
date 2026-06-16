<#
.SYNOPSIS
  CanastaCR dev task runner.
.EXAMPLE
  .\scripts\run.ps1 api              # run API in watch mode
  .\scripts\run.ps1 api:build        # build API (Release)
  .\scripts\run.ps1 api:test         # run all backend tests
  .\scripts\run.ps1 api:coverage     # run backend tests + generate coverage (Cobertura)
  .\scripts\run.ps1 app              # run Flutter app in Chrome
  .\scripts\run.ps1 app:build        # flutter build web (Release)
  .\scripts\run.ps1 app:test         # run Flutter unit tests
  .\scripts\run.ps1 app:coverage     # run Flutter tests + generate coverage (lcov)
  .\scripts\run.ps1 scraper:run      # run scraper service (http://localhost:5050/hangfire)
  .\scripts\run.ps1 scraper:test     # run scraper unit tests (mocked, fast)
  .\scripts\run.ps1 scraper:test:live # run scraper tests against real store websites
  .\scripts\run.ps1 scraper:logs:tail # follow today's local scraper log file live
  .\scripts\run.ps1 infra:validate   # validate Bicep without deploying
  .\scripts\run.ps1 infra:deploy     # deploy Bicep to Azure (prod)
  .\scripts\run.ps1 coverage         # generate coverage for both (api + app)
  .\scripts\run.ps1 help             # show this list
#>
param([string]$Command = 'help')

$Root       = Split-Path $PSScriptRoot -Parent
$Flutter    = 'C:\Users\eorte\source\repos\flutter\bin\flutter'
$ApiSrc     = Join-Path $Root 'api\src\CanastaCR.Api'
$ApiSln     = Join-Path $Root 'api'
$AppDir     = Join-Path $Root 'app'
$InfraDir   = Join-Path $Root 'infra'
$ScraperSrc = Join-Path $Root 'scraper\src\CanastaCR.Scraper'
$ScraperSln = Join-Path $Root 'scraper'

function Assert-EnvVar([string]$Name) {
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

    'api:coverage' {
        Write-Host "▶ Running backend tests with coverage" -ForegroundColor Cyan
        $CoverageDir = "$ApiSln\coverage"
        dotnet test "$ApiSln\tests\CanastaCR.Tests" `
            --collect:"XPlat Code Coverage" `
            --results-directory "$CoverageDir\raw" `
            --logger "console;verbosity=normal"
        # Flatten GUID subfolder → stable path for Coverage Gutters
        $xml = Get-ChildItem "$CoverageDir\raw" -Recurse -Filter "coverage.cobertura.xml" |
               Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($xml) {
            Copy-Item $xml.FullName "$CoverageDir\cobertura.xml" -Force
            Write-Host ""
            Write-Host "✓ Coverage report: api/coverage/cobertura.xml" -ForegroundColor Green
            Write-Host "  In VSCode: Coverage Gutters → Display Coverage (Ctrl+Shift+P)" -ForegroundColor DarkGray
        }
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

    'app:coverage' {
        Write-Host "▶ Running Flutter tests with coverage" -ForegroundColor Cyan
        Push-Location $AppDir
        & $Flutter test --coverage test/models/
        Pop-Location
        if (Test-Path "$AppDir\coverage\lcov.info") {
            Write-Host ""
            Write-Host "✓ Coverage report: app/coverage/lcov.info" -ForegroundColor Green
            Write-Host "  In VSCode: Coverage Gutters → Display Coverage (Ctrl+Shift+P)" -ForegroundColor DarkGray
        }
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
        Assert-EnvVar 'AZURE_RESOURCE_GROUP'
        Assert-EnvVar 'POSTGRES_ADMIN_PASSWORD'
        Assert-EnvVar 'JWT_SECRET'
        az deployment group validate `
            --resource-group $env:AZURE_RESOURCE_GROUP `
            --template-file "$InfraDir\main.bicep" `
            --parameters "$InfraDir\main.bicepparam"
    }

    'infra:deploy' {
        Write-Host "▶ Deploying infra to Azure (prod)" -ForegroundColor Yellow
        Assert-EnvVar 'AZURE_RESOURCE_GROUP'
        Assert-EnvVar 'POSTGRES_ADMIN_PASSWORD'
        Assert-EnvVar 'JWT_SECRET'
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
        Assert-EnvVar 'AZURE_RESOURCE_GROUP'
        az deployment group list `
            --resource-group $env:AZURE_RESOURCE_GROUP `
            --query "[0].properties.outputs" -o json
    }

    'log:tail' {
        Write-Host "▶ Streaming API logs (Ctrl+C to stop)" -ForegroundColor Cyan
        az webapp log tail --name canastacr-api-prod --resource-group canastacr-rg
    }

    'infra:stop' {
        Write-Host "▶ Stopping Azure resources (saves ~$13/mo)" -ForegroundColor Yellow
        az webapp stop  --name canastacr-api-prod --resource-group canastacr-rg
        az postgres flexible-server stop --name canastacr-db-prod --resource-group canastacr-rg
        Write-Host "✓ App Service and PostgreSQL stopped" -ForegroundColor Green
    }

    'infra:start' {
        Write-Host "▶ Starting Azure resources" -ForegroundColor Cyan
        az webapp start --name canastacr-api-prod --resource-group canastacr-rg
        az postgres flexible-server start --name canastacr-db-prod --resource-group canastacr-rg
        Write-Host "✓ App Service and PostgreSQL started" -ForegroundColor Green
    }

    'infra:delete' {
        $Rg = $env:AZURE_RESOURCE_GROUP ?? 'canastacr-rg'
        Write-Host "⚠ This will permanently delete resource group '$Rg' and ALL resources inside it." -ForegroundColor Red
        $confirm = Read-Host "Type the resource group name to confirm"
        if ($confirm -ne $Rg) { Write-Host "Aborted." -ForegroundColor Yellow; exit 0 }
        az group delete --name $Rg --yes --no-wait
        Write-Host "✓ Deletion triggered (runs in background, takes ~2 min)" -ForegroundColor Green
    }

    'infra:delete-resource' {
        param([string]$ResourceId)
        if (-not $ResourceId) {
            Write-Host "Usage: .\run.ps1 infra:delete-resource --ResourceId <resource-id-or-name>" -ForegroundColor Yellow
            Write-Host "Get IDs with: az resource list --resource-group canastacr-rg --output table" -ForegroundColor DarkGray
            exit 1
        }
        Write-Host "⚠ Deleting resource: $ResourceId" -ForegroundColor Red
        $confirm = Read-Host "Confirm? (yes/no)"
        if ($confirm -ne 'yes') { Write-Host "Aborted." -ForegroundColor Yellow; exit 0 }
        az resource delete --ids $ResourceId
    }

    # ── Scraper ───────────────────────────────────────────────────────────
    'scraper' {
        Write-Host "▶ Running scraper (http://localhost:5050/hangfire)" -ForegroundColor Cyan
        dotnet run --project $ScraperSrc --urls "http://localhost:5050"
    }

    'scraper:run' {
        Write-Host "▶ Running scraper (http://localhost:5050/hangfire)" -ForegroundColor Cyan
        dotnet run --project $ScraperSrc --urls "http://localhost:5050"
    }

    'scraper:build' {
        Write-Host "▶ Building scraper (Release)" -ForegroundColor Cyan
        dotnet build $ScraperSln --configuration Release
    }

    'scraper:test' {
        Write-Host "▶ Running scraper tests (mocked — fast, no network)" -ForegroundColor Cyan
        dotnet test "$ScraperSln\tests\CanastaCR.Scraper.Tests" --filter "Category!=Live" --logger "console;verbosity=normal"
    }

    'scraper:test:live' {
        Write-Host "▶ Running scraper LIVE tests (hits real store websites — ~25 products/store)" -ForegroundColor Cyan
        dotnet test "$ScraperSln\tests\CanastaCR.Scraper.Tests" --filter "Category=Live" --logger "console;verbosity=normal"
    }

    'scraper:logs:tail' {
        $LogDir = "$ScraperSrc\logs"
        $Today = Get-Date -Format 'yyyyMMdd'
        $LogFile = "$LogDir\scrape-$Today.log"
        if (-not (Test-Path $LogFile)) {
            Write-Host "No log file yet for today at $LogFile" -ForegroundColor Yellow
            Write-Host "Run '.\scripts\run.ps1 scraper:run' first, or check $LogDir for other dates." -ForegroundColor DarkGray
            exit 0
        }
        Write-Host "▶ Tailing $LogFile (Ctrl+C to stop)" -ForegroundColor Cyan
        Get-Content -Path $LogFile -Wait -Tail 50
    }

    # ── Git hooks ─────────────────────────────────────────────────────────
    'hooks:install' {
        Write-Host "▶ Installing git hooks from .githooks/" -ForegroundColor Cyan
        git config core.hooksPath .githooks
        Write-Host "✓ Hooks active — tests will run automatically before each commit" -ForegroundColor Green
    }

    'hooks:uninstall' {
        Write-Host "▶ Removing git hooks" -ForegroundColor Yellow
        git config --unset core.hooksPath
        Write-Host "✓ Hooks removed" -ForegroundColor Green
    }

    # ── Combined ──────────────────────────────────────────────────────────
    'test' {
        Write-Host "▶ Running all tests (backend + Flutter)" -ForegroundColor Cyan
        dotnet test "$ApiSln\tests\CanastaCR.Tests" --logger "console;verbosity=normal"
        Push-Location $AppDir
        & $Flutter test test/models/
        Pop-Location
    }

    'coverage' {
        Write-Host "▶ Generating coverage for backend + Flutter" -ForegroundColor Cyan
        # Backend
        $CoverageDir = "$ApiSln\coverage"
        dotnet test "$ApiSln\tests\CanastaCR.Tests" `
            --collect:"XPlat Code Coverage" `
            --results-directory "$CoverageDir\raw" `
            --logger "console;verbosity=normal"
        $xml = Get-ChildItem "$CoverageDir\raw" -Recurse -Filter "coverage.cobertura.xml" |
               Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($xml) { Copy-Item $xml.FullName "$CoverageDir\coverage.cobertura.xml" -Force }
        # Flutter
        Push-Location $AppDir
        & $Flutter test --coverage test/models/
        Pop-Location
        Write-Host ""
        Write-Host "✓ Coverage files ready:" -ForegroundColor Green
        Write-Host "    api/coverage/coverage.cobertura.xml" -ForegroundColor White
        Write-Host "    app/coverage/lcov.info" -ForegroundColor White
        Write-Host "  In VSCode: Coverage Gutters → Display Coverage (Ctrl+Shift+P)" -ForegroundColor DarkGray
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
        Write-Host "    api:coverage  Run tests + write api/coverage/coverage.cobertura.xml"
        Write-Host "    api:publish   Publish to api/publish/"
        Write-Host ""
        Write-Host "  Flutter" -ForegroundColor Yellow
        Write-Host "    app           Run in Chrome"
        Write-Host "    app:build     flutter build web --release"
        Write-Host "    app:test      Run unit tests (30)"
        Write-Host "    app:coverage  Run tests + write app/coverage/lcov.info"
        Write-Host "    app:analyze   flutter analyze"
        Write-Host ""
        Write-Host "  Scraper" -ForegroundColor Yellow
        Write-Host "    scraper:run        Run scraper service (port 5050, /hangfire dashboard)"
        Write-Host "    scraper:build      Build scraper (Release)"
        Write-Host "    scraper:test       Run scraper xUnit tests (mocked, fast, no network)"
        Write-Host "    scraper:test:live  Run live tests against real store sites (~25 products/store)"
        Write-Host "    scraper:logs:tail  Follow today's local scraper log file live"
        Write-Host ""
        Write-Host "  Infrastructure" -ForegroundColor Yellow
        Write-Host "    infra:validate         Validate Bicep (no deploy)"
        Write-Host "    infra:deploy           Deploy to Azure (needs env vars)"
        Write-Host "    infra:outputs          Show last deployment outputs"
        Write-Host "    infra:stop             Stop App Service + PostgreSQL (save cost)"
        Write-Host "    infra:start            Start App Service + PostgreSQL"
        Write-Host "    infra:delete           Delete entire resource group (irreversible)"
        Write-Host "    infra:delete-resource  Delete a single resource by ID"
        Write-Host "    log:tail               Stream live API logs from Azure"
        Write-Host ""
        Write-Host "  Git hooks" -ForegroundColor Yellow
        Write-Host "    hooks:install    Activate pre-commit test enforcement"
        Write-Host "    hooks:uninstall  Remove hooks"
        Write-Host ""
        Write-Host "  Combined" -ForegroundColor Yellow
        Write-Host "    test          Run backend + Flutter tests"
        Write-Host "    coverage      Run both + write coverage files for Coverage Gutters"
        Write-Host ""
        Write-Host "  Env vars for infra commands:" -ForegroundColor DarkGray
        Write-Host "    `$env:AZURE_RESOURCE_GROUP   = 'canastacr-rg'"
        Write-Host "    `$env:AZURE_LOCATION         = 'canadacentral'  # optional, default: canadacentral"
        Write-Host "    `$env:POSTGRES_ADMIN_PASSWORD = '...'"
        Write-Host "    `$env:JWT_SECRET              = '...'"
        Write-Host ""
    }
}
