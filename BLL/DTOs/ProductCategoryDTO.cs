using System;

namespace Kiddopay.BLL.DTOs
{
    /// <summary>
    /// Represents a product category returned by <c>GetCategories</c>.
    /// Consumed by the Angular catalog modal's category sidebar.
    /// </summary>
    public class ProductCategoryDTO
    {
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = "";

        /// <summary>
        /// Optional image URL shown as the category icon in the sidebar.
        /// Maps to <c>blser_imageurl</c> on the <c>blser_productcategory</c> table.
        /// </summary>
        public string ImageUrl { get; set; } = "";
    }
}