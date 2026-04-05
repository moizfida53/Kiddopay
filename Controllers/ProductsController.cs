using Kiddopay.Controllers;
using KiddoPay.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;

namespace KiddoPay.API.Controllers
{
    public class ProductsController(IProductService productService) : LocalBaseController
    {
        private readonly IProductService _products = productService;

        /// <summary>
        /// Scans a product barcode and validates it for a specific student.
        /// Returns the product + any warning (AllergyAlert | LowBalance | ForbiddenCategory | None).
        /// Angular calls this every time the cashier scans a new item barcode.
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
        /// Returns all active product categories.
        /// Called once when the catalog modal is first opened so the sidebar can be populated.
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
        /// Returns all available products belonging to the given category.
        /// Called when the cashier clicks a category in the catalog modal sidebar.
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