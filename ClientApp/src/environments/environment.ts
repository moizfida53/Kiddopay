export const environment = {
  production: false,
  apiUrl: 'https://localhost:7255',
  azureAd:{
    clientId:'9282fb62-edc3-4edb-93e3-074aa5e35d80',
    tenantId:'d7be1e00-bf67-40de-a606-1a75a207d413',
    redirectUri:'https://localhost:44440',
    authority:'https://login.microsoftonline.com/d7be1e00-bf67-40de-a606-1a75a207d413',
    scopes:['api://083e71be-e35a-4908-bb91-2be4ab637bee/access_as_user']
  }
};
