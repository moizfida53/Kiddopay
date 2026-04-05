import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Navbar } from './navbar/navbar';
import { inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MsalBroadcastService, MsalService } from '@azure/msal-angular';
import {
  EventMessage,
  EventType,
  AuthenticationResult,
} from '@azure/msal-browser';
import { filter } from 'rxjs/operators';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Navbar],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  currentUser = {
    name: 'Mazen Mahmoud',
    role: 'seller',
    Page: 'preorder',
  };

  private router = inject(Router);
  private msalService = inject(MsalService);
  private broadcastService = inject(MsalBroadcastService);

  isLoading = false;
  errorMessage: string | null = null;

  ngOnInit(): void {
    if (this.isLoggedIn()) {
      this.afterLogin();
      return;
    }

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
    this.isLoading = true;
    this.errorMessage = null;
    this.msalService.loginRedirect({
      scopes: environment.azureAd.scopes,
    });
  }

  isLoggedIn(): boolean {
    return this.msalService.instance.getActiveAccount() != null;
  }

  private afterLogin(): void {
    this.router.navigate(['/scanner']);
  }
}
