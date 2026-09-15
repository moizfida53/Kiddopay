import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from './baseApi.service';
import { Product, ScannedProductResult } from './kiddopay.models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private api = inject(BaseApiService);

  /**
   * Validates a scanned barcode for the current student.
   * Matches: GET /Products/ScanProduct?barcode=...&studentId=...&currentCartTotal=...
   * (LocalBaseController: [controller]/[action])
   */
  scanBarcode(
    barcode: string,
    studentId: string,
    currentCartTotal: number,
  ): Observable<ScannedProductResult> {
    return this.api.get<ScannedProductResult>('/Products/ScanProduct', {
      params: {
        barcode,
        studentId,
        currentCartTotal: currentCartTotal.toString(),
      },
    });
  }

  /**
   * Returns safe alternatives in the same category for this student.
   * Matches: GET /Products/GetSafeAlternatives?productCategoryId=...&studentId=...
   */
  getSafeAlternatives(
    productCategoryId: string,
    studentId: string,
  ): Observable<Product[]> {
    return this.api.get<Product[]>('/Products/GetSafeAlternatives', {
      params: { productCategoryId, studentId },
    });
  }
}
