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

  selectedCategory = signal<ProductCategory | null>(null);
  searchQuery = signal('');
  addedProductIds = signal<Set<string>>(new Set());

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