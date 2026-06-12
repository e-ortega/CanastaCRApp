param location string
param name string

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

output defaultHostname string = staticWebApp.properties.defaultHostname
output resourceId string = staticWebApp.id
@description('Deployment token — used by GitHub Actions. Treat as a secret.')
output apiKey string = listSecrets(staticWebApp.id, staticWebApp.apiVersion).properties.apiKey
