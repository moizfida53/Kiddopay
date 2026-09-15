import {
  Component,
  inject,
  signal,
  Output,
  EventEmitter,
  OnInit,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ItemsService } from 'src/app/services/items.service';
import { StudentService } from 'src/app/services/student.service';
import {
  ProductCatalogService,
  ProductCategory,
} from 'src/app/services/product-catalog.service';
import { Product } from 'src/app/services/kiddopay.models';

@Component({
  selector: 'app-add-item-modal',
  imports: [CommonModule, FormsModule],
  templateUrl: './add-item-modal.html',
  styleUrl: './add-item-modal.scss',
})
export class AddItemModal implements OnInit {
  /** Emitted when the cashier closes the modal (neutral close or confirm) */
  @Output() closed = new EventEmitter<void>();

  public readonly itemsService = inject(ItemsService);
  public readonly catalogService = inject(ProductCatalogService);
  public readonly studentService = inject(StudentService);

  selectedCategory = signal<ProductCategory | null>(null);
  searchQuery = signal('');
  addedProductIds = signal<Set<string>>(new Set());

  // ─── Wallet balance guard ──────────────────────────────────────────────────
  // The cashier can only add items that fit within the student's remaining
  // wallet balance. "Remaining" = wallet balance minus what's already in the
  // new-items cart (this modal only ever adds to newOrderCartItems).

  private static readonly BALANCE_EPSILON = 0.0005; // guards against float rounding at the KD boundary

  remainingWalletBalance = computed(() => {
    const walletBalance = this.studentService.currentStudent()?.walletBalance ?? 0;
    return walletBalance - this.itemsService.newOrderTotal();
  });

  // ─── Daily spend limit guard ────────────────────────────────────────────────
  // Same idea as the wallet guard above, but against the student's daily spend
  // limit: dailySpendLimit - what's already spent today - what's already in this
  // new-items cart. A dailySpendLimit of 0 (or unset) means "no daily limit" —
  // matches the convention OrderService.cs uses server-side.

  hasDailyLimit = computed(() => (this.studentService.currentStudent()?.dailySpendLimit ?? 0) > 0);

  remainingDailyLimit = computed(() => {
    const student = this.studentService.currentStudent();
    const dailySpendLimit = student?.dailySpendLimit ?? 0;
    if (dailySpendLimit <= 0) return Infinity;
    const dailySpentToday = student?.dailySpentToday ?? 0;
    return dailySpendLimit - dailySpentToday - this.itemsService.newOrderTotal();
  });

  private wouldExceedBalance(price: number): boolean {
    return price > this.remainingWalletBalance() + AddItemModal.BALANCE_EPSILON;
  }

  private wouldExceedDailyLimit(price: number): boolean {
    const remaining = this.remainingDailyLimit();
    return remaining !== Infinity && price > remaining + AddItemModal.BALANCE_EPSILON;
  }

  private wouldExceedLimit(price: number): boolean {
    return this.wouldExceedBalance(price) || this.wouldExceedDailyLimit(price);
  }

  /** Used by the template to grey out / disable a catalog product card. */
  canAffordProduct(product: Product): boolean {
    return !this.wouldExceedLimit(product.price);
  }

  /** Used by the template to disable the "+" button on an already-added item. */
  canIncreaseQty(productId: string): boolean {
    const item = this.itemsService
      .newOrderCartItems()
      .find((i) => i.productId === productId);
    if (!item) return true;
    return !this.wouldExceedLimit(item.unitPrice);
  }

  /** Which guard (if any) is currently blocking this price — drives overlay/aria-label text. */
  private blockedReasonForPrice(price: number): 'balance' | 'daily-limit' | null {
    if (this.wouldExceedBalance(price)) return 'balance';
    if (this.wouldExceedDailyLimit(price)) return 'daily-limit';
    return null;
  }

  blockedReason(product: Product): 'balance' | 'daily-limit' | null {
    return this.blockedReasonForPrice(product.price);
  }

  qtyBlockedReason(productId: string): 'balance' | 'daily-limit' | null {
    const item = this.itemsService
      .newOrderCartItems()
      .find((i) => i.productId === productId);
    if (!item) return null;
    return this.blockedReasonForPrice(item.unitPrice);
  }

  ngOnInit(): void {
    this.catalogService.loadCategories();
  }

  // ─── Category selection ───────────────────────────────────────────────────

  selectCategory(cat: ProductCategory): void {
    this.selectedCategory.set(cat);
    this.searchQuery.set('');
    this.catalogService.loadProductsByCategory(cat.categoryId);
  }

  // ─── Product search filter ────────────────────────────────────────────────

  filteredProducts = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    const products = this.catalogService.productsForCategory();
    if (!q) return products;
    return products.filter(
      (p) =>
        p.name.toLowerCase().includes(q) ||
        p.categoryName.toLowerCase().includes(q),
    );
  });

  // ─── Add to cart ──────────────────────────────────────────────────────────

  addProduct(product: Product): void {
    // Defense in depth — the card's [disabled] binding already blocks this
    // click, but guard here too in case that state is stale for a tick.
    if (!product.isAvailable || this.wouldExceedLimit(product.price)) return;

    this.itemsService.addProductToNewOrder(product);

    // Flash the added state for visual feedback
    this.addedProductIds.update((set) => new Set([...set, product.productId]));
    setTimeout(() => {
      this.addedProductIds.update((set) => {
        const next = new Set(set);
        next.delete(product.productId);
        return next;
      });
    }, 1200);
  }

  isAdded(productId: string): boolean {
    return this.addedProductIds().has(productId);
  }

  /** Quantity of this product already in the new-order cart */
  cartQty(productId: string): number {
    return (
      this.itemsService
        .newOrderCartItems()
        .find((i) => i.productId === productId)?.quantity ?? 0
    );
  }

  // ─── Qty controls (from scanned panel) ───────────────────────────────────

  increaseQty(productId: string): void {
    if (!this.canIncreaseQty(productId)) return;
    this.itemsService.increaseNewOrderQty(productId);
  }

  decreaseQty(productId: string): void {
    this.itemsService.decreaseNewOrderQty(productId);
  }

  // ─── Confirm / Cancel ─────────────────────────────────────────────────────

  /**
   * Confirm Order — closes the modal and returns the user to the page.
   * The items remain in the new-order cart so the cashier can proceed.
   */
  confirmOrder(): void {
    this.closed.emit();
  }

  /**
   * Cancel Order — clears all new-order cart items (unselects everything)
   * then closes the modal.
   */
  cancelOrder(): void {
    // Clear all new-order items and their visible cards
    this.itemsService.newOrderCartItems.set([]);
    this.itemsService.newOrderVisibleCards.set(new Set());
    this.closed.emit();
  }

  // ─── Modal close ──────────────────────────────────────────────────────────

  close(): void {
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.close();
    }
  }

  onSearchChange(value: string): void {
    this.searchQuery.set(value);
  }
}