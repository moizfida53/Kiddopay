namespace Kiddopay.BLL.DTOs
{
    public class PreOrderDTO
    {
        public Guid PreOrderId { get; set; }
        public string Reference { get; set; }           // PRE-20250223-0001
        public Guid StudentId { get; set; }
        public string StudentName { get; set; }
        public string ScheduledDate { get; set; }
        public string Status { get; set; }              // Active | PartiallyFulfilled | FullyFulfilled | Cancelled
        public decimal TotalPaid { get; set; }
        public decimal TotalFulfilled { get; set; }
        public List<PreOrderLineDTO> Lines { get; set; } = new();
    }
}
