using Kiddopay.BLL.DTOs;
using System;
using System.Collections.Generic;

namespace KiddoPay.BLL.Interfaces
{
    public interface IProductService
    {
        // ── Barcode scan ──────────────────────────────────────────────────────

        ProductDTO GetProductByBarcode(string barcode);

        ScannedProductResultDTO ValidateProductForStudent(
            string barcode, Guid studentId, decimal currentCartTotal);

        // ── Allergy alternatives ──────────────────────────────────────────────

        List<ProductDTO> GetSafeAlternatives(Guid productCategoryId, Guid studentId);

        // ── Catalog browsing (used by the Add-Item modal) ─────────────────────

        /// <summary>Returns all active product categories for the sidebar.</summary>
        List<ProductCategoryDTO> GetCategories();

        /// <summary>Returns all products in a given category for the product grid.</summary>
        List<ProductDTO> GetProductsByCategory(Guid categoryId);
    }
}