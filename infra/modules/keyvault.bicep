param location string
param keyVaultName string
@secure()
param jwtSecret string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true   // use role assignments, not access policies
    softDeleteRetentionInDays: 7
    enableSoftDelete: true
  }
}

resource jwtSecretEntry 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'jwt-secret'
  properties: { value: jwtSecret }
}

// db-connection-string is stored by the appservice module after postgres is provisioned
resource dbConnPlaceholder 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'db-connection-string'
  properties: { value: 'placeholder' }  // overwritten by appservice module
}

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output keyVaultResourceId string = keyVault.id
