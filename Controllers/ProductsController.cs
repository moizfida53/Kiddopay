using Kiddopay.Controllers;
using KiddoPay.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KiddoPay.API.Controllers
{
    /// <summary>
    /// Routes follow LocalBaseController convention: [controller]/[action]
    ///   GET /Products/ScanProduct?barcode=...&amp;studentId=...&amp;currentCartTotal=...
    ///   GET /Products/GetSafeAlternatives?productCategoryId=...&amp;studentId=...
    ///   GET /Products/GetCategories
    ///   GET /Products/GetProductsByCategory?categoryId=...
    /// </summary>
    public class ProductsController(IProductService productService) : LocalBaseController
    {
        private readonly IProductService _products = productService;

        /// <summary>
        /// Validates a scanned barcode for a specific student.
        /// Returns the product + any warning (AllergyAlert | LowBalance | ForbiddenCategory | None).
        /// </summary>
        [HttpGet]
        public IActionResult ScanProduct(
            [FromQuery] string barcode,
            [FromQuery] Guid studentId,
            [FromQuery] decimal currentCartTotal = 0)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(barcode))
                    return BadRequest(new { message = "Barcode is required." });

                if (studentId == Guid.Empty)
                    return BadRequest(new { message = "StudentId is required." });

                var result = _products.ValidateProductForStudent(barcode, studentId, currentCartTotal);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Returns safe product alternatives in the same category for this student.
        /// Called when the cashier taps "View Safe Alternatives" on an allergy alert.
        /// </summary>
        [HttpGet]
        public IActionResult GetSafeAlternatives(
            [FromQuery] Guid productCategoryId,
            [FromQuery] Guid studentId)
        {
            try
            {
                if (productCategoryId == Guid.Empty)
                    return BadRequest(new { message = "ProductCategoryId is required." });

                if (studentId == Guid.Empty)
                    return BadRequest(new { message = "StudentId is required." });

                var alternatives = _products.GetSafeAlternatives(productCategoryId, studentId);
                return Ok(alternatives);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Returns all active product categories for the catalog modal sidebar.
        /// </summary>
        [HttpGet]
        public IActionResult GetCategories()
        {
            try
            {
                var categories = _products.GetCategories();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Returns all available products in a given category.
        /// </summary>
        [HttpGet]
        public IActionResult GetProductsByCategory([FromQuery] Guid categoryId)
        {
            try
            {
                if (categoryId == Guid.Empty)
                    return BadRequest(new { message = "CategoryId is required." });

                var products = _products.GetProductsByCategory(categoryId);
                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}