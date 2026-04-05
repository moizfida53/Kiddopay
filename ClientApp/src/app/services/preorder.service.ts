import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { BaseApiService } from './baseApi.service';
import { PreOrder } from './kiddopay.models';

@Injectable({ providedIn: 'root' })
export class PreOrderService {
  private api = inject(BaseApiService);

  activePreOrder = signal<PreOrder | null>(null);

  getById(preOrderId: string): Observable<PreOrder> {
    console.log('[PreOrderService] getById → START', { preOrderId });

    return this.api.get<PreOrder>(`/api/PreOrders/${preOrderId}`).pipe(
      tap({
        next: (po) => {
          console.log('[PreOrderService] getById → SUCCESS', po);
          this.activePreOrder.set(po);
        },
        error: (err) => {
          console.error('[PreOrderService] getById → ERROR', err);
        },
        complete: () => {
          console.log('[PreOrderService] getById → COMPLETE');
        },
      }),
    );
  }

loadActivePreOrder(studentId: string): Observable<PreOrder> {
    console.log('[PreOrderService] loadActivePreOrder → START');

    return this.api
      .get<PreOrder>(`/PreOrders/GetActivePreOrder?studentId=${studentId}`)
      .pipe(
        tap({
          next: (po) => {
            console.log('[PreOrderService] loadActivePreOrder → SUCCESS', po);
            this.activePreOrder.set(po);
          },
          error: (err) => console.error('[PreOrderService] loadActivePreOrder → ERROR', err),
        })
      );
  }

  clear(): void {
    console.log('[PreOrderService] clear → RESET activePreOrder');
    this.activePreOrder.set(null);
  }
}
