import { Routes } from '@angular/router';
import { PreorderPage } from './preorder-page/preorder-page';
import { ScannerMain } from './scanner-main/scanner-main';
import { MsalGuard } from '@azure/msal-angular';
import { LoginComponent } from 'src/login/login';
import { App } from './app';

const routes: Routes = [
  // {
  //   path: '',
  //   component: App,
  //   title: 'Login Page',
  // },
  { path: 'scanner', component: ScannerMain, canActivate: [MsalGuard] },
  { path: 'preorder', component: PreorderPage },
];
export default routes;
