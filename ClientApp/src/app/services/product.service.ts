import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './baseApi.service';
import { Product, ScannedProductResult } from './kiddopay.models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private api = inject(BaseApiService);

  /**
   * Validates a scanned barcode for the current student.
   * Returns the product + any warning (AllergyAlert | LowBalance | ForbiddenCategory | None).
   *
   * @param barcode          EAN/UPC string from the scanner hardware
   * @param studentId        Current student's GUID
   * @param currentCartTotal Running total of items already in the cart
   */
  scanBarcode(
    barcode: string,
    studentId: string,
    currentCartTotal: number
  ): Observable<ScannedProductResult> {
    return this.api.get<ScannedProductResult>('/api/Products/scan', {
      params: { barcode, studentId, currentCartTotal: currentCartTotal.toString() },
    });
  }

  /**
   * Returns safe alternatives in the same category for this student.
   * Called when the cashier taps "View Safe Alternatives" on an allergy alert.
   */
  getSafeAlternatives(
    productCategoryId: string,
    studentId: string
  ): Observable<Product[]> {
    return this.api.get<Product[]>('/api/Products/alternatives', {
      params: { productCategoryId, studentId },
    });
  }
}