export const environment = {
  production: true,
  // Angular is served as static files by the same ASP.NET Core app in
  // production (wwwroot) -- so API calls are same-origin, relative paths.
  // No separate host to configure per environment, unlike apiUrl in dev.
  apiUrl: '',
  azureAd: {
    clientId: '9282fb62-edc3-4edb-93e3-074aa5e35d80',
    tenantId: 'd7be1e00-bf67-40de-a606-1a75a207d413',
    // Computed at runtime instead of hardcoded, so the same production build
    // works on whichever host it ends up on (Azure App Service, Render, a
    // custom domain later) without touching this file again. IMPORTANT: the
    // exact production URL this resolves to (e.g. https://kiddopay.azurewebsites.net
    // or https://kiddopay.onrender.com) must also be added as a Redirect URI
    // on this app registration in Entra ID -- App registrations -> your app ->
    // Authentication -> Redirect URIs -- or sign-in will fail with an
    // AADSTS50011 mismatch error.
    redirectUri: window.location.origin,
    authority: 'https://login.microsoftonline.com/d7be1e00-bf67-40de-a606-1a75a207d413',
    scopes: ['api://083e71be-e35a-4908-bb91-2be4ab637bee/access_as_user']
  }
};
