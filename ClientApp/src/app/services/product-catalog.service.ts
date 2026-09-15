import { Injectable, inject, signal } from '@angular/core';
import { BaseApiService } from './baseApi.service';
import { Product } from './kiddopay.models';

export interface ProductCategory {
  categoryId: string;
  categoryName: string;
  imageUrl?: string;
  productCount?: number;
}

@Injectable({ providedIn: 'root' })
export class ProductCatalogService {
  private api = inject(BaseApiService);

  // ─── Signals ───────────────────────────────────────────────────────────────

  categories = signal<ProductCategory[]>([]);
  productsForCategory = signal<Product[]>([]);

  categoriesLoading = signal(false);
  productsLoading = signal(false);

  categoriesError = signal<string | null>(null);
  productsError = signal<string | null>(null);

  // ─── Categories ────────────────────────────────────────────────────────────

  /**
   * Loads all active product categories from the API.
   * Matches: GET /Products/GetCategories
   * (LocalBaseController convention: [controller]/[action])
   * Result is cached — subsequent calls are no-ops until clearProducts() is called.
   */
  loadCategories(): void {
    if (this.categories().length > 0) return;

    this.categoriesLoading.set(true);
    this.categoriesError.set(null);

    this.api.get<ProductCategory[]>('/Products/GetCategories').subscribe({
      next: cats => {
        this.categories.set(cats ?? []);
        this.categoriesLoading.set(false);
      },
      error: err => {
        console.error('[ProductCatalogService] loadCategories error', err);
        this.categoriesError.set('Could not load categories. Please try again.');
        this.categoriesLoading.set(false);
      },
    });
  }

  // ─── Products by category ──────────────────────────────────────────────────

  /**
   * Loads all available products for the given category.
   * Matches: GET /Products/GetProductsByCategory?categoryId=...
   */
  loadProductsByCategory(categoryId: string): void {
    this.productsLoading.set(true);
    this.productsError.set(null);
    this.productsForCategory.set([]);

    this.api
      .get<Product[]>(`/Products/GetProductsByCategory?categoryId=${categoryId}`)
      .subscribe({
        next: products => {
          this.productsForCategory.set(products ?? []);
          this.productsLoading.set(false);
        },
        error: err => {
          console.error('[ProductCatalogService] loadProductsByCategory error', err);
          this.productsError.set('Could not load products. Please try again.');
          this.productsLoading.set(false);
        },
      });
  }

  /** Clear product list when the modal is closed and re-opened. */
  clearProducts(): void {
    this.productsForCategory.set([]);
    this.productsError.set(null);
  }

  /** Full reset — call when switching students. */
  reset(): void {
    this.categories.set([]);
    this.productsForCategory.set([]);
    this.categoriesError.set(null);
    this.productsError.set(null);
  }
}
