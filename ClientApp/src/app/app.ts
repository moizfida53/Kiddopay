import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Router } from '@angular/router';
import { MsalBroadcastService, MsalService } from '@azure/msal-angular';
import { EventMessage, EventType, AuthenticationResult } from '@azure/msal-browser';
import { filter } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { Navbar } from './navbar/navbar';
import { CashierService } from './services/cashier.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Navbar],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private router = inject(Router);
  private msalService = inject(MsalService);
  private broadcastService = inject(MsalBroadcastService);
  private cashierService = inject(CashierService);

  // Reactive profile for the navbar — populated after loadProfile() resolves
  cashierProfile = this.cashierService.profile;

  isLoading = false;
  errorMessage: string | null = null;

  ngOnInit(): void {
    if (this.isLoggedIn()) {
      this.afterLogin();
      return;
    }

    this.broadcastService.msalSubject$
      .pipe(filter((msg: EventMessage) => msg.eventType === EventType.LOGIN_SUCCESS))
      .subscribe((msg: EventMessage) => {
        const result = msg.payload as AuthenticationResult;
        if (result?.account) {
          this.msalService.instance.setActiveAccount(result.account);
        }
        this.afterLogin();
      });

    this.isLoading = true;
    this.errorMessage = null;
    this.msalService.loginRedirect({ scopes: environment.azureAd.scopes });
  }

  isLoggedIn(): boolean {
    return this.msalService.instance.getActiveAccount() != null;
  }

  /**
   * Called once the user is authenticated.
   * Load the cashier profile first so cashierId + storeId are available
   * before any order is submitted.
   */
  private afterLogin(): void {
    this.cashierService.loadProfile().subscribe({
      next: profile => console.log('[App] Cashier profile loaded:', profile.displayName),
      error: err => console.error('[App] Failed to load cashier profile:', err),
    });
    this.router.navigate(['/scanner']);
  }
}
