namespace Kiddopay.BLL.DTOs
{
    // ─── Pre-Order ─────────────────────────────────────────────────────────────

    public class PreOrderLineDTO
    {
        public Guid PreOrderLineId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string ImageUrl { get; set; }
        public string? ImageBase64 { get; set; }
        public int QuantityOrdered { get; set; }
        public int QuantityFulfilled { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineAmount { get; set; }
        public string LineStatus { get; set; }   // Pending | PartiallyFulfilled | Fulfilled | Cancelled
        public bool IsFullyFulfilled => QuantityFulfilled >= QuantityOrdered;
    }
}
