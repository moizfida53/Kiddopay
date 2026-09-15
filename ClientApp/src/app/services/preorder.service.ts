import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { BaseApiService } from './baseApi.service';
import { PreOrder } from './kiddopay.models';

@Injectable({ providedIn: 'root' })
export class PreOrderService {
  private api = inject(BaseApiService);

  activePreOrder = signal<PreOrder | null>(null);

  /**
   * Fetches a pre-order by its GUID.
   * Matches: GET /PreOrders/GetPreOrderById/{preOrderId}
   * (LocalBaseController uses [controller]/[action] routing)
   */
  getById(preOrderId: string): Observable<PreOrder> {
    return this.api
      .get<PreOrder>(`/PreOrders/GetPreOrderById/${preOrderId}`)
      .pipe(
        tap({
          next: po => this.activePreOrder.set(po),
          error: err => console.error('[PreOrderService] getById error', err),
        }),
      );
  }

  /**
   * Loads the active pre-order for a student.
   * Matches: GET /PreOrders/GetActivePreOrder?studentId=...
   * (LocalBaseController convention: [controller]/[action])
   */
  loadActivePreOrder(studentId: string): Observable<PreOrder> {
    return this.api
      .get<PreOrder>(`/PreOrders/GetActivePreOrder?studentId=${studentId}`)
      .pipe(
        tap({
          next: po => this.activePreOrder.set(po),
          error: err => console.error('[PreOrderService] loadActivePreOrder error', err),
        }),
      );
  }

  clear(): void {
    this.activePreOrder.set(null);
  }
}
