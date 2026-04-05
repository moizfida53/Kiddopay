import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { BaseApiService } from './baseApi.service';
import { Product } from './kiddopay.models';

export interface ProductCategory {
  categoryId: string;
  categoryName: string;
  /** Optional icon/emoji or image URL — shown in the sidebar */
  imageUrl?: string;
  productCount?: number;
}
const MOCK_CATEGORIES: ProductCategory[] = [
  {
    categoryId: '1',
    categoryName: 'Burgers',
    imageUrl: 'https://picsum.photos/300/200?random=1',
    productCount: 12
  },
  {
    categoryId: '2',
    categoryName: 'Pizzas',
    imageUrl: 'https://picsum.photos/300/200?random=2',
    productCount: 8
  },
  {
    categoryId: '3',
    categoryName: 'Drinks',
    imageUrl: 'https://picsum.photos/300/200?random=3',
    productCount: 15
  },
  {
    categoryId: '4',
    categoryName: 'Desserts',
    imageUrl: 'https://picsum.photos/300/200?random=4',
    productCount: 6
  },
  {
    categoryId: '5',
    categoryName: 'Sandwiches',
    imageUrl: 'https://picsum.photos/300/200?random=5',
    productCount: 10
  }
];

const mockProducts: Product[] = [
  {
    productId: 'p1',
    name: 'Cheese Burger',
    categoryName: 'Burgers',
    categoryId: '1', // ✅ FIXED
    price: 2.5,
    imageUrl: 'https://images.unsplash.com/photo-1568901346375-23c9450c58cd',
    barcode: '111111111111',
    isAvailable: true,
  },
  {
    productId: 'p3',
    name: 'French Fries',
    categoryName: 'Snacks',
    categoryId: '2',
    price: 1.2,
    imageUrl: 'https://images.unsplash.com/photo-1541592106381-b31e9677c0e5',
    barcode: '333333333333',
    isAvailable: true,
  },
  {
    productId: 'p4',
    name: 'Orange Juice',
    categoryName: 'Drinks',
    categoryId: '3',
    price: 1.0,
    imageUrl: 'https://images.unsplash.com/photo-1582719478250-c89cae4dc85b',
    barcode: '444444444444',
    isAvailable: true,
  },
  {
    productId: 'p5',
    name: 'Chocolate Cake',
    categoryName: 'Desserts',
    categoryId: '4',
    price: 2.0,
    imageUrl: 'https://images.unsplash.com/photo-1605478371310-a9f1e96b4ff4',
    barcode: '555555555555',
    isAvailable: false,
  }
];

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

  // loadCategories(): void {
  //   if (this.categories().length > 0) return; // already loaded — skip
  //   this.categoriesLoading.set(true);
  //   this.categoriesError.set(null);

  //   this.api.get<ProductCategory[]>('/api/Products/categories').subscribe({
  //     next: (cats) => {
  //       this.categories.set(cats ?? []);
  //       this.categoriesLoading.set(false);
  //       console.log('[ProductCatalogService] Categories loaded:', cats?.length);
  //     },
  //     error: (err) => {
  //       console.error('[ProductCatalogService] loadCategories error', err);
  //       this.categoriesError.set('Could not load categories.');
  //       this.categoriesLoading.set(false);
  //     },
  //   });
  // }
  loadCategories(): void {
  if (this.categories().length > 0) return;

  this.categoriesLoading.set(true);
  this.categoriesError.set(null);

  // 🔥 Simulate API delay
  setTimeout(() => {
    try {
      this.categories.set(MOCK_CATEGORIES);
      console.log('[ProductCatalogService] Categories loaded:', MOCK_CATEGORIES.length);
    } catch (err) {
      console.error('[ProductCatalogService] loadCategories error', err);
      this.categoriesError.set('Could not load categories.');
    } finally {
      this.categoriesLoading.set(false);
    }
  }, 800); // simulate network delay
}

  // ─── Products by category ──────────────────────────────────────────────────

  // loadProductsByCategory(categoryId: string): void {
  //   this.productsLoading.set(true);
  //   this.productsError.set(null);
  //   this.productsForCategory.set([]);

  //   this.api
  //     .get<Product[]>(`/api/Products/by-category?categoryId=${categoryId}`)
  //     .subscribe({
  //       next: (products) => {
  //         this.productsForCategory.set(products ?? []);
  //         this.productsLoading.set(false);
  //         console.log(
  //           `[ProductCatalogService] Products loaded for ${categoryId}:`,
  //           products?.length,
  //         );
  //       },
  //       error: (err) => {
  //         console.error('[ProductCatalogService] loadProductsByCategory error', err);
  //         this.productsError.set('Could not load products.');
  //         this.productsLoading.set(false);
  //       },
  //     });
  // }

  loadProductsByCategory(categoryId: string): void {
  this.productsLoading.set(true);
  this.productsError.set(null);

  // simulate API delay
  setTimeout(() => {
    const filtered = mockProducts.filter(p => p.categoryId === categoryId);

    this.productsForCategory.set(filtered);
    this.productsLoading.set(false);

    console.log(
      `[Mock] Products loaded for ${categoryId}:`,
      filtered.length
    );
  }, 500);
}

  /** Clear product list (e.g. when the modal is closed and re-opened) */
  clearProducts(): void {
    this.productsForCategory.set([]);
    this.productsError.set(null);
  }
}