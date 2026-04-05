import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import { Router } from '@angular/router';
import { from, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import {
  InteractionRequiredAuthError,
  BrowserAuthError,
} from '@azure/msal-browser';
import { environment } from 'src/environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const msal = inject(MsalService);
  const router = inject(Router);

  const account =
    msal.instance.getActiveAccount() ?? msal.instance.getAllAccounts()[0];

  // No account at all — let the request through unauthenticated.
  // The msalGuard on protected routes will redirect to login before any
  // authenticated HTTP call is even made, so this branch is mainly for
  // public endpoints (health checks, login page assets, etc.).
  if (!account) {
    return next(req);
  }

  const tokenRequest = {
    scopes: environment.azureAd.scopes,
    account,
  };

  return from(msal.acquireTokenSilent(tokenRequest)).pipe(
    switchMap((response) => {
      let headers = req.headers.set(
        'Authorization',
        `Bearer ${response.accessToken}`,
      );

      if (['POST', 'PUT', 'PATCH'].includes(req.method.toUpperCase())) {
        headers = headers
          .set('Content-Type', 'application/json')
          .set('Accept', 'application/json');
      }

      return next(req.clone({ headers }));
    }),

    catchError((err) => {
      // ── timed_out ────────────────────────────────────────────────────────
      // MSAL hasn't finished hydrating the LocalStorage cache yet. This
      // should not happen if initializeAppData() awaited initialize() + 
      // handleRedirectPromise() properly. If it does, redirect to login so
      // the user gets a clean token — do NOT try acquireTokenPopup here
      // because the browser will block it as an unsolicited popup.
      if (
        err instanceof BrowserAuthError &&
        err.errorCode === 'timed_out'
      ) {
        console.warn(
          '[authInterceptor] timed_out — MSAL not ready. Redirecting to login.',
        );
        msal.loginRedirect({ scopes: environment.azureAd.scopes });
        return throwError(() => err);
      }

      // ── InteractionRequired ───────────────────────────────────────────────
      // The token exists but is expired or requires MFA consent. Use redirect
      // (not popup) to avoid browser popup blockers.
      if (err instanceof InteractionRequiredAuthError) {
        console.warn('[authInterceptor] Interaction required — redirecting.');
        msal.loginRedirect({
          scopes: environment.azureAd.scopes,
          account,
        });
        return throwError(() => err);
      }

      console.error('[authInterceptor] Unhandled MSAL error:', err);
      return throwError(() => err);
    }),
  );
};