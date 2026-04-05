import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MsalBroadcastService, MsalService } from '@azure/msal-angular';
import { EventMessage, EventType, AuthenticationResult } from '@azure/msal-browser';
import { filter } from 'rxjs/operators';
import { CashierService } from 'src/app/services/cashier.service';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './login.html',
  styleUrls: ['./login.scss'],
})
export class LoginComponent implements OnInit {
  private router = inject(Router);
  private msalService = inject(MsalService);
  private broadcastService = inject(MsalBroadcastService);
  private cashierService = inject(CashierService);

  isLoading = false;
  errorMessage: string | null = null;

  ngOnInit(): void {
    // If the user is already authenticated (e.g. returning after a refresh
    // with LocalStorage cache intact) skip the login page entirely.
    if (this.isLoggedIn()) {
      this.afterLogin();
      return;
    }

    // Listen for the redirect callback. MSAL emits LOGIN_SUCCESS after
    // handleRedirectPromise() resolves with an account in app.config.ts.
    // By the time this component renders that promise has already been
    // awaited, so this handles the case where the component is rendered
    // right after the redirect lands (before the guard re-runs).
    this.broadcastService.msalSubject$
      .pipe(
        filter(
          (msg: EventMessage) => msg.eventType === EventType.LOGIN_SUCCESS,
        ),
      )
      .subscribe((msg: EventMessage) => {
        const result = msg.payload as AuthenticationResult;
        if (result?.account) {
          this.msalService.instance.setActiveAccount(result.account);
        }
        this.afterLogin();
      });
  }

  onMicrosoftLogin(): void {
    this.isLoading = true;
    this.errorMessage = null;

    // Use redirect (not popup) — consistent with the guard and interceptor.
    // The user will leave the page and return after Azure AD authenticates them.
    // handleRedirectPromise() in app.config.ts picks up the result on return.
    this.msalService.loginRedirect({
      scopes: environment.azureAd.scopes,
    });
  }

  isLoggedIn(): boolean {
    return this.msalService.instance.getActiveAccount() != null;
  }

  private afterLogin(): void {
    // Load the cashier profile from Dataverse (/api/Cashier/me) before
    // navigating so the scanner page has cashierId + storeId ready immediately.
    // this.cashierService.loadProfile();
    this.router.navigate(['/scanner']);
  }
}