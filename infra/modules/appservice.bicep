param location string
param appName string
param postgresFqdn string
param postgresAdminLogin string
@secure()
param postgresAdminPassword string
param postgresDatabaseName string = 'canastacr'
param appInsightsConnectionString string
@secure()
param jwtSecret string

var connectionString = 'Host=${postgresFqdn};Database=${postgresDatabaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require;Trust Server Certificate=true'

// App Service Plan (B1)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  sku: { name: 'B1', tier: 'Basic' }
  kind: 'linux'
  properties: { reserved: true }  // required for Linux
}

// App Service
resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      appSettings: [
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'Jwt__Issuer', value: 'canastacr' }
        { name: 'Jwt__Audience', value: 'canastacr' }
        { name: 'Jwt__Key', value: jwtSecret }
        { name: 'ConnectionStrings__DefaultConnection', value: connectionString }
      ]
      cors: {
        allowedOrigins: ['*']  // tighten once SWA URL is known
        supportCredentials: false
      }
    }
    httpsOnly: true
  }
}

output defaultHostname string = webApp.properties.defaultHostName
output appName string = webApp.name
output principalId string = webApp.identity.principalId
