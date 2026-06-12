targetScope = 'resourceGroup'

@description('Short env name appended to all resource names. E.g. "prod" or "staging".')
param environmentName string = 'prod'

param location string = resourceGroup().location

@description('PostgreSQL admin username.')
param administratorLogin string = 'canastacradmin'

@secure()
@description('PostgreSQL admin password. Passed via --parameters in CI.')
param administratorLoginPassword string

@secure()
@description('JWT signing secret (≥32 chars). Passed via --parameters in CI.')
param jwtSecret string

// ── Resource names ─────────────────────────────────────────────────────────
var apiAppName    = 'canastacr-api-${environmentName}'
var postgresName  = 'canastacr-db-${environmentName}'
var keyVaultName  = 'canastacr-kv-${environmentName}'
var swaName       = 'canastacr-web-${environmentName}'

// ── Modules ────────────────────────────────────────────────────────────────
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    environmentName: environmentName
  }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    keyVaultName: keyVaultName
    jwtSecret: jwtSecret
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    location: location
    serverName: postgresName
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
  }
}

module appservice 'modules/appservice.bicep' = {
  name: 'appservice'
  params: {
    location: location
    appName: apiAppName
    keyVaultName: keyVaultName
    keyVaultResourceId: keyvault.outputs.keyVaultResourceId
    postgresFqdn: postgres.outputs.fqdn
    postgresAdminLogin: administratorLogin
    postgresAdminPassword: administratorLoginPassword
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
  dependsOn: [keyvault, postgres, monitoring]
}

module staticwebapp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp'
  params: {
    location: location
    name: swaName
  }
}

// ── Outputs ────────────────────────────────────────────────────────────────
output apiUrl string = 'https://${appservice.outputs.defaultHostname}'
output webUrl string = 'https://${staticwebapp.outputs.defaultHostname}'
output keyVaultName string = keyvault.outputs.keyVaultName
output postgresServerName string = postgres.outputs.serverName
output appServiceName string = appservice.outputs.appName
output staticWebAppName string = staticwebapp.outputs.resourceId
