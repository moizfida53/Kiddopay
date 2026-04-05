namespace Kiddopay.BLL.DTOs
{
    public class CartSummaryDTO
    {
        public Guid StudentId { get; set; }
        public decimal WalletBalance { get; set; }
        public decimal DailySpendLimit { get; set; }
        public decimal DailySpentToday { get; set; }
        public List<CartItemDTO> Items { get; set; } = new();
        public decimal Total => Items.Sum(i => i.LineTotal);
        public bool IsBalanceSufficient => WalletBalance >= Total;
        public bool IsDailyLimitExceeded => (DailySpentToday + Total) > DailySpendLimit;
    }
}
