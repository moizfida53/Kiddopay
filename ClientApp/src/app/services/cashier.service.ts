import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { BaseApiService } from './baseApi.service';
import { CashierProfile } from './kiddopay.models';

@Injectable({ providedIn: 'root' })
export class CashierService {
  private api = inject(BaseApiService);

  /** Populated once after Microsoft SSO login. Used by all order requests. */
  profile = signal<CashierProfile | null>(null);

  /**
   * Reads the Azure AD OID from the JWT and returns the matching cashier record.
   * Matches: GET /api/Cashier/me
   * Returns an Observable so callers (e.g. AppComponent) can react or catch errors.
   */
  loadProfile(): Observable<CashierProfile> {
    return this.api
      .get<CashierProfile>('/api/Cashier/me')
      .pipe(tap(p => this.profile.set(p)));
  }

  get cashierId(): string {
    return this.profile()?.cashierId ?? '';
  }

  get storeId(): string {
    return this.profile()?.storeId ?? '';
  }
}
