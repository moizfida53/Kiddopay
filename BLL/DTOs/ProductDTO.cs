namespace Kiddopay.BLL.DTOs
{

    // ─── Product ───────────────────────────────────────────────────────────────

    public class ProductDTO
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; }
        public string CategoryName { get; set; }
        public Guid CategoryId { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public string Barcode { get; set; }
        public bool IsAvailable { get; set; }
    }
}
