namespace Kiddopay.BLL.DTOs
{
    // ─── Student / NFC Scan ────────────────────────────────────────────────────

    public class StudentProfileDTO
    {
        public Guid StudentId { get; set; }
        public string FullName { get; set; }
        public string Grade { get; set; }           // e.g. "2/A3"
        public string AvatarUrl { get; set; }
        public decimal WalletBalance { get; set; }
        public decimal DailySpendLimit { get; set; }
        public decimal DailySpentToday { get; set; }
        public decimal RemainingDailyBudget => DailySpendLimit - DailySpentToday;
        public bool HasActivePreOrder { get; set; }
        public Guid? ActivePreOrderId { get; set; }
    }
}
