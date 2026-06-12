using 'main.bicep'

// Non-secret parameters. Secrets (administratorLoginPassword, jwtSecret)
// are injected via --parameters in CI (GitHub Actions secrets) — never commit them here.

param environmentName = 'prod'
param location        = 'eastus2'
param administratorLogin = 'canastacradmin'
