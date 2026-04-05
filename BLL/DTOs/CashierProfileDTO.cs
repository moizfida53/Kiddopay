namespace Kiddopay.BLL.DTOs
{
    // ─── Cashier Auth ──────────────────────────────────────────────────────────

    public class CashierProfileDTO
    {
        public Guid CashierId { get; set; }
        public string DisplayName { get; set; }
        public string JobTitle { get; set; }
        public string Email { get; set; }
        public Guid StoreId { get; set; }
        public string StoreName { get; set; }
    }
}
