namespace Kiddopay.BLL.DTOs
{
    public class OrderLineRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool IsFromPreOrder { get; set; }
        public Guid? PreOrderLineId { get; set; }
    }
}
