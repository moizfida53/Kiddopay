using Kiddopay.BLL.DTOs;

namespace Kiddopay.BLL.DTOs
{
    // ─── Complete / Confirm Order Requests ────────────────────────────────────

    public class CompleteOrderRequest
    {
        public Guid StudentId { get; set; }
        public Guid CashierId { get; set; }
        public Guid StoreId { get; set; }
        public Guid? PreOrderId { get; set; }           // null for direct-scan orders
        public List<OrderLineRequest> Lines { get; set; } = new();
        /// <summary>DirectScan | PreOrderFulfillment | Mixed</summary>
        public string OrderType { get; set; }
    }
}
