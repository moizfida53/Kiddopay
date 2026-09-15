namespace Kiddopay.BLL.DTOs
{
    // ─── Parent registration / login / OTP verification ───────────────────────
    // These are the request/response shapes the mobile app talks to directly --
    // see ParentsController for the actual routes. None of the Dataverse field
    // names below leak into these contracts; the mobile developer only needs
    // this file's shapes.

    public class RegisterParentRequest
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string Password { get; set; }
    }

    public class VerifyEmailRequest
    {
        public string Email { get; set; }
        public string OtpCode { get; set; }
    }

    public class ResendOtpRequest
    {
        public string Email { get; set; }

        /// <summary>"Registration" or "ForgotPassword" -- which flow the resend is for.</summary>
        public string Purpose { get; set; }
    }

    public class LoginParentRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; }
        public string OtpCode { get; set; }
        public string NewPassword { get; set; }
    }

    /// <summary>
    /// Returned by /login and /verify-email -- the mobile app stores Token and
    /// sends it as "Authorization: Bearer {Token}" on every subsequent call.
    /// </summary>
    public class ParentAuthResponse
    {
        public string Token { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public Guid ParentId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
    }
}
