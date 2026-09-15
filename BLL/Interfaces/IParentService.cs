using Kiddopay.BLL.DTOs;

namespace Kiddopay.BLL.Interfaces
{
    public interface IParentService
    {
        /// <summary>
        /// Creates the parent record (unverified) and emails a 6-digit OTP.
        /// If an unverified registration already exists for this email, it's
        /// refreshed (new password, new OTP) rather than rejected, so someone
        /// who abandoned verification can just register again. A *verified*
        /// existing email is rejected.
        /// </summary>
        void Register(RegisterParentRequest request);

        /// <summary>
        /// Confirms the registration OTP, marks the account verified, and logs
        /// the parent straight in (returns a token) -- matches the "register
        /// -> OTP screen -> into the app" flow.
        /// </summary>
        ParentAuthResponse VerifyEmail(VerifyEmailRequest request);

        /// <summary>Regenerates and re-sends the OTP for the given purpose.</summary>
        void ResendOtp(ResendOtpRequest request);

        ParentAuthResponse Login(LoginParentRequest request);

        /// <summary>
        /// Always succeeds from the caller's point of view (doesn't reveal
        /// whether the email exists) -- silently no-ops if there's no
        /// matching account, sends a reset OTP if there is.
        /// </summary>
        void ForgotPassword(ForgotPasswordRequest request);

        /// <summary>
        /// Confirms the reset OTP and sets the new password. Deliberately does
        /// NOT return a token -- the app should send the parent back to a
        /// normal login with their new password.
        /// </summary>
        void ResetPassword(ResetPasswordRequest request);
    }
}
