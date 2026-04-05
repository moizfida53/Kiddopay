import { Injectable, inject, signal } from '@angular/core';
import { tap } from 'rxjs';
import { BaseApiService } from './baseApi.service';
import { CashierProfile } from './kiddopay.models';

@Injectable({ providedIn: 'root' })
export class CashierService {
  private api = inject(BaseApiService);

  // Populated once after Microsoft SSO login — used by all order requests
  profile = signal<CashierProfile | null>(null);

  /**
   * Call once in AppComponent (or an auth guard) after the user logs in.
   * Reads the Azure AD OID from the JWT and returns the matching cashier record.
   */
  loadProfile(): void {
    this.api
      .get<CashierProfile>('/api/Cashier/me')
      .pipe(tap((p) => this.profile.set(p)))
      .subscribe();
  }

  get cashierId(): string {
    return this.profile()?.cashierId ?? '';
  }

  get storeId(): string {
    return this.profile()?.storeId ?? '';
  }
}