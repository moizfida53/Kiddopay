import {
  APP_INITIALIZER,
  ApplicationConfig,
  inject,
  provideZoneChangeDetection,
} from '@angular/core';
import { HTTP_INTERCEPTORS, withInterceptors } from '@angular/common/http';
import {
  MsalInterceptor,
  MsalGuard,
  MsalService,
  MsalBroadcastService,
  MSAL_INSTANCE,
  MSAL_GUARD_CONFIG,
  MsalGuardConfiguration,
} from '@azure/msal-angular';
import {
  PublicClientApplication,
  InteractionType,
  BrowserCacheLocation,
  EventType,
} from '@azure/msal-browser';
import { provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import routes from './app.routes';
import { environment } from 'src/environments/environment';
import { authInterceptor } from './auth.interceptor';

// ─── MSAL instance — created once and reused everywhere ─────────────────────
//
// Important: this must be a singleton. If MSALInstanceFactory() is called more
// than once (e.g. because the provider is accidentally duplicated) you will get
// two MSAL instances fighting over the same LocalStorage keys, which also
// causes timed_out errors.
//
let msalInstance: PublicClientApplication | null = null;

export function MSALInstanceFactory(): PublicClientApplication {
  if (!msalInstance) {
    msalInstance = new PublicClientApplication({
      auth: {
        clientId: environment.azureAd.clientId,
        authority: environment.azureAd.authority,
        redirectUri: environment.azureAd.redirectUri,
      },
      cache: {
        // LocalStorage keeps the user logged in across tabs and browser
        // restarts. The key is making sure initialize() + handleRedirectPromise()
        // both fully complete BEFORE any HTTP call fires — see initializeAppData.
        cacheLocation: BrowserCacheLocation.LocalStorage,
      },
    });
  }
  return msalInstance;
}

// ─── Guard config ─────────────────────────────────────────────────────────────
export function MSALGuardConfigFactory(): MsalGuardConfiguration {
  return {
    interactionType: InteractionType.Redirect,
    authRequest: {
      scopes: environment.azureAd.scopes,
    },
  };
}

// ─── App initializer ─────────────────────────────────────────────────────────
//
// This runs BEFORE the router activates any route, which means it runs before
// any service makes an HTTP call, which means the interceptor always has a
// valid account by the time acquireTokenSilent() is called.
//
// The critical addition vs the original: we return a Promise that does NOT
// resolve until MSAL has finished the full LocalStorage cache read AND the
// redirect promise is settled. Previously the function returned void which
// made Angular treat it as synchronously complete.
//
export function initializeAppData() {
  return async (): Promise<void> => {
    const msalService = inject(MsalService);

    // Step 1 — initialize() reads the LocalStorage cache and builds the
    // internal token store. With LocalStorage this is async; with
    // SessionStorage it completes almost instantly (empty cache).
    await msalService.instance.initialize();

    // Step 2 — after a redirect login the browser returns to the app with
    // an auth code in the URL. handleRedirectPromise() exchanges that code
    // for tokens. This MUST be awaited before any silent token acquire.
    const result = await msalService.instance.handleRedirectPromise();

    // Step 3 — set the active account from the redirect result, or fall back
    // to whatever account was already in the cache.
    if (result?.account) {
      msalService.instance.setActiveAccount(result.account);
    } else {
      const accounts = msalService.instance.getAllAccounts();
      if (accounts.length > 0) {
        msalService.instance.setActiveAccount(accounts[0]);
      }
    }

    // At this point MSAL is fully ready. Any subsequent acquireTokenSilent()
    // call will find the cache warm and resolve without a timed_out error.
  };
}

// ─── Application config ───────────────────────────────────────────────────────
export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),

    // NOTE: Do NOT also provide MsalInterceptor via HTTP_INTERCEPTORS here.
    // You already have authInterceptor (the functional one) doing token
    // attachment above. Providing MsalInterceptor as a class interceptor
    // alongside it means EVERY request gets the Authorization header attached
    // TWICE — once by each interceptor. Remove the block below if you had it.
    //
    // { provide: HTTP_INTERCEPTORS, useClass: MsalInterceptor, multi: true },

    {
      provide: MSAL_INSTANCE,
      useFactory: MSALInstanceFactory,
    },
    {
      provide: MSAL_GUARD_CONFIG,
      useFactory: MSALGuardConfigFactory,
    },
    {
      provide: APP_INITIALIZER,
      useFactory: initializeAppData,
      deps: [MsalService],  // explicit dep ensures MsalService is resolved first
      multi: true,
    },

    MsalService,
    MsalGuard,
    MsalBroadcastService,
  ],
};