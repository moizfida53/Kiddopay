using KiddoPay.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;

namespace KiddoPay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CashierController(ICashierService cashierService) : ControllerBase
    {
        private readonly ICashierService _cashier = cashierService;

        /// <summary>
        /// Returns the cashier's profile using the Azure AD OID from the JWT token.
        /// Angular calls this once after Microsoft SSO login to populate the top-right header
        /// (Mazen Mahmoud / Saller) and to obtain the CashierId + StoreId needed
        /// for order requests.
        /// </summary>
        [HttpGet("me")]
        public IActionResult GetMyProfile()
        {
            try
            {
                // Extract Azure AD OID from the JWT claims
                var oid = User.FindFirstValue("oid")
                       ?? User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

                if (string.IsNullOrWhiteSpace(oid))
                    return Unauthorized(new { message = "Azure AD OID not found in token." });

                var profile = _cashier.GetCashierByMicrosoftOid(oid);
                return Ok(profile);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
