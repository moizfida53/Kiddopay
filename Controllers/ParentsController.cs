using Kiddopay.BLL.DTOs;
using Kiddopay.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kiddopay.API.Controllers
{
    /// <summary>
    /// Parent registration / login / OTP verification for the mobile app.
    /// Deliberately NOT [Authorize] at the class level -- every action here
    /// runs before a parent has a token (that's the whole point), unlike
    /// every other controller in this project.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class ParentsController(IParentService parentService) : ControllerBase
    {
        private readonly IParentService _parents = parentService;

        /// <summary>POST /api/Parents/register</summary>
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterParentRequest request)
        {
            try
            {
                _parents.Register(request);
                return Ok(new { message = "Registered. Check your email for a verification code." });
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>POST /api/Parents/verify-email -- confirms the registration OTP and logs the parent in.</summary>
        [HttpPost("verify-email")]
        public IActionResult VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            try
            {
                var result = _parents.VerifyEmail(request);
                return Ok(result);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>POST /api/Parents/resend-otp -- purpose is "Registration" or "ForgotPassword".</summary>
        [HttpPost("resend-otp")]
        public IActionResult ResendOtp([FromBody] ResendOtpRequest request)
        {
            try
            {
                _parents.ResendOtp(request);
                return Ok(new { message = "A new code has been sent." });
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>POST /api/Parents/login</summary>
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginParentRequest request)
        {
            try
            {
                var result = _parents.Login(request);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        /// <summary>
        /// POST /api/Parents/forgot-password -- always responds with the same
        /// generic message, whether or not the email is on file, so a caller
        /// can't use this to discover which emails are registered.
        /// </summary>
        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                _parents.ForgotPassword(request);
            }
            catch (Exception)
            {
                // Swallowed on purpose, for the same reason as above -- a
                // transient failure here shouldn't leak account existence
                // either. It's still logged by the framework's default
                // exception logging.
            }

            return Ok(new { message = "If an account exists for this email, a reset code has been sent." });
        }

        /// <summary>POST /api/Parents/reset-password</summary>
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                _parents.ResetPassword(request);
                return Ok(new { message = "Password updated. Please log in." });
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }
    }
}
