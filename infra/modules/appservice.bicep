param location string
param appName string
param keyVaultName string
param keyVaultResourceId string
param postgresFqdn string
param postgresAdminLogin string
@secure()
param postgresAdminPassword string
param postgresDatabaseName string = 'canastacr'
param appInsightsConnectionString string

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
        // Key Vault references — resolved at runtime via managed identity
        {
          name: 'Jwt__Key'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=jwt-secret)'
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=db-connection-string)'
        }
      ]
      cors: {
        allowedOrigins: ['*']  // tighten once SWA URL is known
        supportCredentials: false
      }
    }
    httpsOnly: true
  }
}

// Write real connection string to Key Vault
resource dbConnSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/db-connection-string'
  properties: { value: connectionString }
}

// Grant the App Service managed identity "Key Vault Secrets User" role
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultResourceId, webApp.id, 'Key Vault Secrets User')
  scope: resourceGroup()
  properties: {
    // Key Vault Secrets User built-in role ID
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [webApp]
}

output defaultHostname string = webApp.properties.defaultHostName
output appName string = webApp.name
output principalId string = webApp.identity.principalId
