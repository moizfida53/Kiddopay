import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { MsalService } from '@azure/msal-angular';

export const msalGuard: CanActivateFn = () => {
  const msal = inject(MsalService);

  let account = msal.instance.getActiveAccount();

  // 🔁 restore session if lost on refresh
  if (!account) {
    const accounts = msal.instance.getAllAccounts();
    if (accounts.length > 0) {
      account = accounts[0];
      msal.instance.setActiveAccount(account);
    }
  }

  // ❌ no session → force login
  if (!account) {
    msal.loginRedirect();
    return false;
  }

  return true;
};