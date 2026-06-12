using 'main.bicep'

// Non-secret params are hardcoded here.
// Secrets are read from environment variables — set them in your shell before deploying:
//   $env:POSTGRES_ADMIN_PASSWORD = "..."
//   $env:JWT_SECRET              = "..."
// In GitHub Actions they are set from repository secrets.

param environmentName        = 'prod'
param location               = 'canadacentral'
param staticWebAppLocation   = 'centralus'    // SWA not available in Canada Central
param administratorLogin     = 'canastacradmin'
param administratorLoginPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
param jwtSecret              = readEnvironmentVariable('JWT_SECRET')
