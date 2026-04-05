import { Injectable, inject, signal, computed, effect } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { BaseApiService } from './baseApi.service';
import { CashierService } from './cashier.service';
import { StudentService } from './student.service';
import { PreOrderService } from './preorder.service';
import { ProductService } from './product.service';
import {
  CartItem,
  CompleteOrderRequest,
  OrderResult,
  PersistedSession,
  PreOrderLine,
  Product,
  ScannedProductResult,
} from './kiddopay.models';

const SESSION_KEY = 'kiddopay_session';

@Injectable({ providedIn: 'root' })
export class ItemsService {
  private api = inject(BaseApiService);
  private cashierService = inject(CashierService);
  private studentService = inject(StudentService);
  private productService = inject(ProductService);
  private preOrderService = inject(PreOrderService);

  // ─── Pre-order cart ────────────────────────────────────────────────────────

  preOrderCartItems = signal<CartItem[]>([]);
  preOrderVisibleCards = signal<Set<string>>(new Set());

  // ─── New-order cart ────────────────────────────────────────────────────────

  newOrderCartItems = signal<CartItem[]>([]);
  newOrderVisibleCards = signal<Set<string>>(new Set());

  // ─── Last scan warning ─────────────────────────────────────────────────────

  lastScanResult = signal<ScannedProductResult | null>(null);

  // ─── Legacy alias ──────────────────────────────────────────────────────────

  selectedItems = computed<CartItem[]>(() => [
    // ...this.preOrderCartItems(),
    ...this.newOrderCartItems(),
  ]);

  // ─── Totals ────────────────────────────────────────────────────────────────

  total = computed(() => {
    const sum = this.selectedItems().reduce((s, i) => s + i.lineTotal, 0);
    console.log(`[ItemsService] Total computed: ${sum}`);
    return sum;
  });

  preOrderTotal = computed(() =>
    this.preOrderCartItems().reduce((s, i) => s + i.lineTotal, 0),
  );

  newOrderTotal = computed(() =>
    this.newOrderCartItems().reduce((s, i) => s + i.lineTotal, 0),
  );

  itemTotal(productId: string): number {
    const item = this.selectedItems().find((i) => i.productId === productId);
    return item ? item.lineTotal : 0;
  }

  // ─── "Please select pre-order" prompt ─────────────────────────────────────

  hasUnselectedPreOrder = computed<boolean>(() => {
    const po = this.preOrderService.activePreOrder();
    if (!po || !po.lines || po.lines.length === 0) return false;
    return this.preOrderCartItems().length === 0;
  });

  // ─── Persistence ───────────────────────────────────────────────────────────

  constructor() {
    effect(() => {
      const studentId = this.studentService.currentStudent()?.studentId;
      if (!studentId) return;
      const session: PersistedSession = {
        studentId,
        preOrderCartItems: this.preOrderCartItems(),
        newOrderCartItems: this.newOrderCartItems(),
        preOrderVisibleCards: [...this.preOrderVisibleCards()],
        newOrderVisibleCards: [...this.newOrderVisibleCards()],
      };
      try {
        sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
      } catch (e) {
        console.warn('[ItemsService] Could not persist session:', e);
      }
    });
  }

  restoreSession(studentId: string): boolean {
    try {
      const raw = sessionStorage.getItem(SESSION_KEY);
      if (!raw) return false;
      const session: PersistedSession = JSON.parse(raw);
      if (session.studentId !== studentId) return false;
      this.preOrderCartItems.set(session.preOrderCartItems ?? []);
      this.newOrderCartItems.set(session.newOrderCartItems ?? []);
      this.preOrderVisibleCards.set(
        new Set(session.preOrderVisibleCards ?? []),
      );
      this.newOrderVisibleCards.set(
        new Set(session.newOrderVisibleCards ?? []),
      );
      console.log('[ItemsService] Session restored for student:', studentId);
      return true;
    } catch (e) {
      console.warn('[ItemsService] Could not restore session:', e);
      return false;
    }
  }

  // ─── Pre-order loading ─────────────────────────────────────────────────────

  // ✅ Fix — just store the pre-order data, don't auto-add to cart
  loadActivePreOrder(studentId: string): void {
    const alreadyRestored = this.restoreSession(studentId);

    this.preOrderService.loadActivePreOrder(studentId).subscribe({
      next: (po) => {
        if (alreadyRestored) return;
        // Just clear the cart — the pre-order lines are available
        // via preOrderService.activePreOrder() for the preorder panel.
        // Items only enter the cart when the cashier selects them.
        this.preOrderCartItems.set([]);
        this.preOrderVisibleCards.set(new Set());
      },
      error: (err) => {
        console.error('[ItemsService] loadActivePreOrder error', err);
        if (!alreadyRestored) {
          this.preOrderCartItems.set([]);
          this.preOrderVisibleCards.set(new Set());
        }
      },
    });
  }

  // ─── Pre-order carousel ────────────────────────────────────────────────────

  getAllItems = computed<PreOrderLineView[]>(() => {
    const po = this.preOrderService.activePreOrder();
    if (!po) return [];
    return po.lines.map((line) => {
      const cartItem = this.preOrderCartItems().find(
        (i) => i.preOrderLineId === line.preOrderLineId,
      );
      const selectedQty = cartItem?.quantity ?? 0;
      return {
        id: line.preOrderLineId,
        name: line.productName,
        displayName: line.productName,
        quantity: line.quantityOrdered,
        remainingQty: line.quantityOrdered - selectedQty,
        img: line.imageBase64 || line.imageUrl || '',
        type: '',
        isSelected: selectedQty > 0,
        isFullySelected: selectedQty >= line.quantityOrdered,
      };
    });
  });

  isItemSelected(preOrderLineId: string): boolean {
    return this.preOrderCartItems().some(
      (i) => i.preOrderLineId === preOrderLineId,
    );
  }

  toggleItem(preOrderLineId: string): void {
    const po = this.preOrderService.activePreOrder();
    if (!po) return;
    const line = po.lines.find((l) => l.preOrderLineId === preOrderLineId);
    if (!line) return;
    const exists =
      this.preOrderCartItems().findIndex(
        (i) => i.preOrderLineId === preOrderLineId,
      ) !== -1;
    if (exists) {
  this.increasePreOrderQty(preOrderLineId);

    } else {
      this.preOrderCartItems.update((items) => [
        ...items,
        this.preOrderLineToCartItem(line),
      ]);
      this.preOrderVisibleCards.update((cards) => {
        const next = new Set(cards);
        next.add(preOrderLineId);
        return next;
      });
    }
  }

  // ─── Pre-order qty controls ────────────────────────────────────────────────

  increasePreOrderQty(preOrderLineId: string): void {
    this.preOrderCartItems.update((items) =>
      items.map((i) => {
        if (i.preOrderLineId !== preOrderLineId) return i;
        if (i.quantity >= (i.maxQuantity ?? Infinity)) return i;
        const q = i.quantity + 1;
        return { ...i, quantity: q, lineTotal: q * i.unitPrice };
      }),
    );
  }

  decreasePreOrderQty(preOrderLineId: string): void {
    const item = this.preOrderCartItems().find(
      (i) => i.preOrderLineId === preOrderLineId,
    );
    if (!item) return;
    if (item.quantity <= 1) {
      this.preOrderCartItems.update((items) =>
        items.filter((i) => i.preOrderLineId !== preOrderLineId),
      );
      this.preOrderVisibleCards.update((cards) => {
        const n = new Set(cards);
        n.delete(preOrderLineId);
        return n;
      });
    } else {
      const q = item.quantity - 1;
      this.preOrderCartItems.update((items) =>
        items.map((i) =>
          i.preOrderLineId === preOrderLineId
            ? { ...i, quantity: q, lineTotal: q * i.unitPrice }
            : i,
        ),
      );
    }
  }

  hidePreOrderCard(preOrderLineId: string): void {
    this.preOrderVisibleCards.update((c) => {
      const n = new Set(c);
      n.delete(preOrderLineId);
      return n;
    });
    this.preOrderCartItems.update((items) =>
      items.filter((i) => i.preOrderLineId !== preOrderLineId),
    );
  }

  // ─── New-order: barcode scan ───────────────────────────────────────────────

  scanAndAdd(barcode: string): Observable<ScannedProductResult> {
    const studentId = this.studentService.currentStudent()?.studentId ?? '';
    return this.productService
      .scanBarcode(barcode, studentId, this.total())
      .pipe(
        tap((result) => {
          this.lastScanResult.set(result);
          if (result.canAdd && result.product) {
            this._addProductToNewOrderCart(result.product);
          }
        }),
      );
  }

  // ─── New-order: manual add from catalog modal ──────────────────────────────

  /**
   * Called by the catalog modal when the cashier taps a product card.
   * Increments quantity if already in cart, otherwise adds a new entry.
   */
  addProductToNewOrder(product: Product): void {
    console.log('[ItemsService] addProductToNewOrder:', product.productId);
    this._addProductToNewOrderCart(product);
  }

  private _addProductToNewOrderCart(product: Product): void {
    const existing = this.newOrderCartItems().find(
      (i) => i.productId === product.productId,
    );
    if (existing) {
      this.newOrderCartItems.update((items) =>
        items.map((i) =>
          i.productId === product.productId
            ? {
                ...i,
                quantity: i.quantity + 1,
                lineTotal: (i.quantity + 1) * i.unitPrice,
              }
            : i,
        ),
      );
    } else {
      const newItem: CartItem = {
        productId: product.productId,
        productName: product.name,
        categoryName: product.categoryName,
        imageUrl: product.imageUrl,
        unitPrice: product.price,
        quantity: 1,
        lineTotal: product.price,
        isFromPreOrder: false,
        preOrderLineId: null,
        maxQuantity: null,
      };
      this.newOrderCartItems.update((items) => [...items, newItem]);
      this.newOrderVisibleCards.update(
        (cards) => new Set([...cards, product.productId]),
      );
    }
  }

  // ─── New-order qty controls ────────────────────────────────────────────────

  increaseNewOrderQty(productId: string): void {
    this.newOrderCartItems.update((items) =>
      items.map((i) =>
        i.productId === productId
          ? {
              ...i,
              quantity: i.quantity + 1,
              lineTotal: (i.quantity + 1) * i.unitPrice,
            }
          : i,
      ),
    );
  }

  decreaseNewOrderQty(productId: string): void {
    const item = this.newOrderCartItems().find(
      (i) => i.productId === productId,
    );
    if (!item) return;
    if (item.quantity <= 1) {
      this.newOrderCartItems.update((items) =>
        items.filter((i) => i.productId !== productId),
      );
      this.newOrderVisibleCards.update((c) => {
        const n = new Set(c);
        n.delete(productId);
        return n;
      });
    } else {
      const q = item.quantity - 1;
      this.newOrderCartItems.update((items) =>
        items.map((i) =>
          i.productId === productId
            ? { ...i, quantity: q, lineTotal: q * i.unitPrice }
            : i,
        ),
      );
    }
  }

  hideNewOrderCard(productId: string): void {
    this.newOrderVisibleCards.update((c) => {
      const n = new Set(c);
      n.delete(productId);
      return n;
    });
    this.newOrderCartItems.update((items) =>
      items.filter((i) => i.productId !== productId),
    );
  }

  // ─── Legacy aliases ────────────────────────────────────────────────────────

  increaseQuantity(productId: string): void {
    const item = this.preOrderCartItems().find(
      (i) => i.productId === productId,
    );
    item?.preOrderLineId
      ? this.increasePreOrderQty(item.preOrderLineId)
      : this.increaseNewOrderQty(productId);
  }

  decreaseQuantity(productId: string): void {
    const item = this.preOrderCartItems().find(
      (i) => i.productId === productId,
    );
    item?.preOrderLineId
      ? this.decreasePreOrderQty(item.preOrderLineId)
      : this.decreaseNewOrderQty(productId);
  }

  hideCard(productId: string): void {
    const isPreOrder = this.preOrderCartItems().some(
      (i) => i.productId === productId || i.preOrderLineId === productId,
    );
    isPreOrder
      ? this.hidePreOrderCard(productId)
      : this.hideNewOrderCard(productId);
  }

  // ─── Complete Order ────────────────────────────────────────────────────────

  completeOrder(): Observable<OrderResult> {
    const student = this.studentService.currentStudent()!;
    const po = this.preOrderService.activePreOrder();
    const allItems = this.selectedItems();
    const hasPreOrder = allItems.some((i) => i.isFromPreOrder);
    const hasFresh = allItems.some((i) => !i.isFromPreOrder);
    const orderType =
      hasPreOrder && hasFresh
        ? 'Mixed'
        : hasPreOrder
          ? 'PreOrderFulfillment'
          : 'DirectScan';

    const request: CompleteOrderRequest = {
      studentId: student.studentId,
      cashierId: this.cashierService.cashierId,
      storeId: this.cashierService.storeId,
      preOrderId: po?.preOrderId ?? null,
      orderType,
      lines: allItems.map((i) => ({
        productId: i.productId,
        quantity: i.quantity,
        unitPrice: i.unitPrice,
        isFromPreOrder: i.isFromPreOrder,
        preOrderLineId: i.preOrderLineId,
      })),
    };

    return this.api
      .post<OrderResult>('/api/Orders/complete', request)
      .pipe(tap(() => this.refreshStudentBalance()));
  }

  cancelOrder(orderId: string): Observable<any> {
    return this.api.post<any>(`/api/Orders/${orderId}/cancel`, {});
  }

  // ─── Session reset ─────────────────────────────────────────────────────────

  resetSession(): void {
    this.preOrderCartItems.set([]);
    this.preOrderVisibleCards.set(new Set());
    this.newOrderCartItems.set([]);
    this.newOrderVisibleCards.set(new Set());
    this.lastScanResult.set(null);
    this.studentService.clearStudent();
    this.preOrderService.clear();
    try {
      sessionStorage.removeItem(SESSION_KEY);
    } catch {}
  }

  // ─── Private helpers ───────────────────────────────────────────────────────

private preOrderLineToCartItem(line: PreOrderLine, quantity = 1): CartItem {
  return {
    productId: line.productId,
    productName: line.productName,
    categoryName: '',
    imageUrl: line.imageBase64 || line.imageUrl || '',
    unitPrice: line.unitPrice,
    quantity: quantity,               // ✅ starts at 1 (or whatever is passed)
    lineTotal: line.unitPrice * quantity,  // ✅ calculated from unitPrice × qty
    isFromPreOrder: true,
    preOrderLineId: line.preOrderLineId,
    maxQuantity: line.quantityOrdered, // ✅ still caps at the full ordered qty
  };
}

  private refreshStudentBalance(): void {
    const studentId = this.studentService.currentStudent()?.studentId;
    if (studentId) this.studentService.refreshStudent(studentId).subscribe();
  }
}

// ─── View models ──────────────────────────────────────────────────────────────

export interface PreOrderLineView {
  id: string;
  name: string;
  displayName: string;
  quantity: number;
  remainingQty: number;
  img: string;
  type: string;
  isSelected: boolean;
  isFullySelected: boolean;
}
