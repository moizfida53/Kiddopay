import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { PreOrderService } from '../services/preorder.service';

@Component({
  selector: 'app-scanner-main',
  imports: [],
  templateUrl: './scanner-main.html',
  styleUrl: './scanner-main.scss',
})
export class ScannerMain {
  router = inject(Router);
  PreorderService = inject(PreOrderService)

  onScanClick() {
    const hasPreorderItems = this.checkPreorderItems();

    if (hasPreorderItems) {
      this.router.navigate(['/preorder']);
    } else {
      this.router.navigate(['/main']);
    }
  }

  checkPreorderItems(): boolean {
  //  this.PreorderService.loadForStudent("eff63907-2816-f111-8341-000d3a6793f9");
   return true
  }
}
