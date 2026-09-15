using Kiddopay.BLL.DTOs;
using Kiddopay.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;

namespace Kiddopay.API.Controllers
{
    // ParentScheme, not the default (Azure AD) scheme -- parents authenticate
    // with the email/password/OTP flow in ParentsController, which issues its
    // own JWT. See Program.cs for where ParentScheme is registered.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "ParentScheme")]
    public class DeviceTokensController(IDeviceTokenService deviceTokenService) : ControllerBase
    {
        private readonly IDeviceTokenService _deviceTokens = deviceTokenService;

        /// <summary>
        /// Called by the mobile app right after it obtains (or refreshes) its
        /// FCM registration token, so the backend knows where to send push
        /// notifications for this parent. The parent identity comes from the
        /// caller's own JWT, not the request body -- a client can only ever
        /// register a token against itself.
        /// </summary>
        [HttpPost]
        public IActionResult RegisterToken([FromBody] RegisterDeviceTokenRequest request)
        {
            try
            {
                var parentId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                _deviceTokens.RegisterToken(parentId, request);
                return Ok();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
