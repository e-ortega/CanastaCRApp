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
// apiKey (deploy token) is NOT output here — retrieve it after deploy with:
//   az staticwebapp secrets list --name <name> --resource-group <rg> --query "properties.apiKey" -o tsv
