namespace Kiddopay.BLL.DTOs
{
    public class OrderResultDTO
    {
        public Guid OrderId { get; set; }
        public string OrderReference { get; set; }
        public string StudentName { get; set; }
        public decimal OrderTotal { get; set; }
        public decimal WalletBalanceBefore { get; set; }
        public decimal WalletBalanceAfter { get; set; }
        public string Status { get; set; }
        public string OrderType { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
