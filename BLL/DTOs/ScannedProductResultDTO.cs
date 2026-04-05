namespace Kiddopay.BLL.DTOs
{
    public class ScannedProductResultDTO
    {
        public ProductDTO Product { get; set; }
        /// <summary>None | AllergyAlert | LowBalance | ForbiddenCategory</summary>
        public string WarningType { get; set; }
        public string WarningMessage { get; set; }
        public bool CanAdd { get; set; }
        public List<ProductDTO> SafeAlternatives { get; set; }
    }
}
