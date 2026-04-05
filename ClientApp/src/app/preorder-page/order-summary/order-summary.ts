import { Component, inject, signal, computed } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ItemsService } from 'src/app/services/items.service';
import { StudentService } from 'src/app/services/student.service';
import { OrderResult } from 'src/app/services/kiddopay.models';

export type PaymentMethod = 'cash' | 'knet' | 'wallet';

@Component({
  selector: 'app-order-summary',
  imports: [DecimalPipe],
  templateUrl: './order-summary.html',
  styleUrl: './order-summary.scss',
})
export class OrderSummary {
  public readonly itemsService = inject(ItemsService);
  public readonly studentService = inject(StudentService);

  // ─── Submitting / error state ─────────────────────────────────────────────

  isSubmitting = signal(false);
  lastOrderResult = signal<OrderResult | null>(null);
  errorMessage = signal<string | null>(null);

  // ─── Payment modal state ──────────────────────────────────────────────────

  showPaymentModal = signal(false);

  /** Which payment methods the cashier has toggled on (multi-select) */
  selectedMethods = signal<PaymentMethod[]>([]);

  /** Amount entered per payment method */
  paymentAmounts = signal<Partial<Record<PaymentMethod, number>>>({});

  paymentValidationError = signal<string | null>(null);

  // ─── Derived: does the current cart contain any new (non-preorder) items? ─

  hasNewItems = computed<boolean>(() =>
    this.itemsService.newOrderCartItems().length > 0,
  );

  // ─── Payment amount helpers ───────────────────────────────────────────────

  enteredTotal = computed<number>(() => {
    const amounts = this.paymentAmounts();
    return (
      (amounts['cash'] ?? 0) +
      (amounts['knet'] ?? 0) +
      (amounts['wallet'] ?? 0)
    );
  });

  isAmountMatched = computed<boolean>(() => {
    const diff = Math.abs(this.enteredTotal() - this.itemsService.total());
    return diff < 0.001 && this.enteredTotal() > 0;
  });

  isAmountOver = computed<boolean>(() =>
    this.enteredTotal() > this.itemsService.total() + 0.001,
  );

  // ─── Payment modal controls ───────────────────────────────────────────────

  openPaymentModal(): void {
    // Reset state fresh each time
    this.selectedMethods.set([]);
    this.paymentAmounts.set({});
    this.paymentValidationError.set(null);
    this.showPaymentModal.set(true);
  }

  closePaymentModal(): void {
    this.showPaymentModal.set(false);
  }

  onPaymentBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('payment-backdrop')) {
      this.closePaymentModal();
    }
  }

  // ─── Method toggle ────────────────────────────────────────────────────────

  toggleMethod(method: PaymentMethod): void {
    this.paymentValidationError.set(null);
    this.selectedMethods.update((methods) => {
      if (methods.includes(method)) {
        // Deselect: also clear its entered amount
        this.paymentAmounts.update((a) => {
          const next = { ...a };
          delete next[method];
          return next;
        });
        return methods.filter((m) => m !== method);
      }
      return [...methods, method];
    });
  }

  isMethodSelected(method: PaymentMethod): boolean {
    return this.selectedMethods().includes(method);
  }

  // ─── Amount input ─────────────────────────────────────────────────────────

  onAmountChange(method: PaymentMethod, raw: string): void {
    this.paymentValidationError.set(null);
    const parsed = parseFloat(raw);
    this.paymentAmounts.update((a) => ({
      ...a,
      [method]: isNaN(parsed) || parsed < 0 ? 0 : parsed,
    }));
  }

  // ─── Confirm payment ──────────────────────────────────────────────────────

  onConfirmPayment(): void {
    this.paymentValidationError.set(null);

    // if (this.selectedMethods().length === 0) {
    //   this.paymentValidationError.set('Please select at least one payment method.');
    //   return;
    // }

    if (this.enteredTotal() <= 0) {
      this.paymentValidationError.set('Please enter the payment amount.');
      return;
    }

    if (this.enteredTotal() < this.itemsService.total() - 0.001) {
      this.paymentValidationError.set(
        `Amount entered (${this.enteredTotal().toFixed(3)} KD) is less than the order total (${this.itemsService.total().toFixed(3)} KD).`,
      );
      return;
    }

    // All valid — submit
    this.closePaymentModal();
    this.onCompleteOrder();
  }

  // ─── Complete order (pre-order only path + post-payment path) ────────────

  onCompleteOrder(): void {
    if (this.isSubmitting()) return;
    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    this.itemsService.completeOrder().subscribe({
      next: (result) => {
        this.lastOrderResult.set(result);
        this.isSubmitting.set(false);
        // Bootstrap success modal is triggered via data-bs-toggle in template
        const modal = document.getElementById('warningModal');
        if (modal) {
          const bsModal = (window as any).bootstrap?.Modal?.getOrCreateInstance(modal);
          bsModal?.show();
        }
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message ?? 'Failed to complete order.');
        this.isSubmitting.set(false);
      },
    });
  }

  /** Called when the cashier cancels the order mid-session */
  onCancelOrder(): void {
    this.itemsService.resetSession();
  }
}