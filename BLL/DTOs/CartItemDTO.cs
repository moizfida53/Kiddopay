namespace Kiddopay.BLL.DTOs
{
    // ─── Cart / Order Lines ────────────────────────────────────────────────────

    public class CartItemDTO
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public string ImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
        public bool IsFromPreOrder { get; set; }
        public Guid? PreOrderLineId { get; set; }
    }
}
