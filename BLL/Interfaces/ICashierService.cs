using Kiddopay.BLL.DTOs;

namespace KiddoPay.BLL.Interfaces
{
    public interface ICashierService
    {
        /// <summary>
        /// Returns the cashier profile matching the Azure AD Object ID (OID).
        /// Throws when no active cashier record exists for that OID.
        /// </summary>
        CashierProfileDTO GetCashierByMicrosoftOid(string microsoftOid);
    }
}
