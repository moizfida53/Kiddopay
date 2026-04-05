import { Injectable, inject } from '@angular/core';
import { MsalService } from '@azure/msal-angular';

@Injectable({ providedIn: 'root' })
export class UserService {
  private msal = inject(MsalService);

  getUserId(): string | null {
    const account = this.msal.instance.getAllAccounts()[0];
    
    if (!account) {
      return null;
    }

    // Get the Object ID (oid) from idTokenClaims
    return account.idTokenClaims?.['oid'] as string ?? account.localAccountId;
  }

  getUserInfo() {
    const account = this.msal.instance.getAllAccounts()[0];
    
    if (!account) {
      return null;
    }

    return {
      userId: account.idTokenClaims?.['oid'] as string,
      username: account.username,
      name: account.name,
      email: account.username,
      tenantId: account.tenantId
    };
  }
}