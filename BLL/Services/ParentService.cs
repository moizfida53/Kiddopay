using Kiddopay.BLL.DTOs;
using Kiddopay.BLL.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using ITokenService = Kiddopay.BLL.Interfaces.ITokenService;
using System.Security.Cryptography;

namespace KiddoPay.BLL.Services
{
    public class ParentService(
        IOrganizationService organizationService,
        ITokenService tokenService,
        IEmailService emailService,
        IConfiguration configuration) : IParentService
    {
        private readonly IOrganizationService _org = organizationService;
        private readonly ITokenService _tokens = tokenService;
        private readonly IEmailService _email = emailService;
        private readonly IConfiguration _config = configuration;

        // ── NOT YET CONFIRMED ───────────────────────────────────────────────
        // These OTP-purpose choice values are a GUESS, following the same
        // auto-numbering pattern Dataverse used for blser_platform on
        // blser_devicetoken (Android=550220000, iOS=550220001) -- i.e. the
        // first two options of a new custom Choice column in this environment.
        // Once the "OTP Purpose" column exists, open it in the maker portal
        // and confirm/correct these two values.
        private const int OtpPurposeRegistration = 550220000;
        private const int OtpPurposeForgotPassword = 550220001;

        public void Register(RegisterParentRequest request)
        {
            if (request == null) throw new ArgumentException("Request body is required.");
            if (string.IsNullOrWhiteSpace(request.FullName)) throw new ArgumentException("Full name is required.");
            if (string.IsNullOrWhiteSpace(request.Email)) throw new ArgumentException("Email is required.");
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
                throw new ArgumentException("Password must be at least 8 characters.");

            var email = request.Email.Trim().ToLowerInvariant();
            var existing = FetchParentByEmail(email);

            // A verified account already owns this email -- reject. An
            // unverified one is treated as an abandoned registration and gets
            // refreshed (new password, new OTP) rather than rejected, so
            // someone who never finished verifying can just register again.
            if (existing != null && existing.GetAttributeValue<bool>("blser_emailverified"))
                throw new InvalidOperationException("An account with this email already exists.");

            var otp = GenerateOtp();
            var otpMinutes = _config.GetSection("ParentAuth").GetValue<int?>("OtpExpiryMinutes") ?? 10;

            var entity = new Entity("blser_parent", existing?.Id ?? Guid.Empty)
            {
                ["blser_name"] = request.FullName.Trim(),
                ["blser_email"] = email,
                ["blser_phonenumber"] = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                ["blser_passwordhash"] = BCrypt.Net.BCrypt.HashPassword(request.Password),
                ["blser_emailverified"] = false,
                ["blser_isactive"] = true,
                ["blser_otpcode"] = otp,
                ["blser_otpexpiresat"] = DateTime.UtcNow.AddMinutes(otpMinutes),
                ["blser_otppurpose"] = new OptionSetValue(OtpPurposeRegistration),
                ["blser_otpattempts"] = 0,
            };

            if (existing != null)
                _org.Update(entity);
            else
                _org.Create(entity);

            SendOtpEmail(email, request.FullName, otp, "complete your registration");
        }

        public ParentAuthResponse VerifyEmail(VerifyEmailRequest request)
        {
            var email = request?.Email?.Trim().ToLowerInvariant();
            var parent = FetchParentByEmail(email)
                ?? throw new ArgumentException("No account found for this email.");

            // Idempotent: a double-tap on "verify" after it already succeeded
            // just logs them in again instead of erroring.
            if (!parent.GetAttributeValue<bool>("blser_emailverified"))
            {
                ValidateOtp(parent, request.OtpCode, OtpPurposeRegistration, "registration");

                _org.Update(new Entity("blser_parent", parent.Id)
                {
                    ["blser_emailverified"] = true,
                    ["blser_otpcode"] = null,
                    ["blser_otpattempts"] = 0,
                });
            }

            return BuildAuthResponse(parent.Id, email, parent.GetAttributeValue<string>("blser_name"));
        }

        public void ResendOtp(ResendOtpRequest request)
        {
            var email = request?.Email?.Trim().ToLowerInvariant();
            var parent = FetchParentByEmail(email)
                ?? throw new ArgumentException("No account found for this email.");

            var purpose = MapPurpose(request.Purpose);
            var otp = GenerateOtp();
            var otpMinutes = _config.GetSection("ParentAuth").GetValue<int?>("OtpExpiryMinutes") ?? 10;

            _org.Update(new Entity("blser_parent", parent.Id)
            {
                ["blser_otpcode"] = otp,
                ["blser_otpexpiresat"] = DateTime.UtcNow.AddMinutes(otpMinutes),
                ["blser_otppurpose"] = new OptionSetValue(purpose),
                ["blser_otpattempts"] = 0,
            });

            var flowLabel = purpose == OtpPurposeForgotPassword ? "reset your password" : "complete your registration";
            SendOtpEmail(email, parent.GetAttributeValue<string>("blser_name"), otp, flowLabel);
        }

        public ParentAuthResponse Login(LoginParentRequest request)
        {
            var email = request?.Email?.Trim().ToLowerInvariant();
            var parent = FetchParentByEmail(email);
            var storedHash = parent?.GetAttributeValue<string>("blser_passwordhash");

            // Same generic message whether the email doesn't exist or the
            // password is wrong -- don't tell an attacker which one it was.
            if (parent == null || string.IsNullOrEmpty(storedHash)
                || !BCrypt.Net.BCrypt.Verify(request?.Password ?? "", storedHash))
                throw new UnauthorizedAccessException("Invalid email or password.");

            if (!parent.GetAttributeValue<bool>("blser_isactive"))
                throw new UnauthorizedAccessException("This account has been disabled.");

            if (!parent.GetAttributeValue<bool>("blser_emailverified"))
                throw new InvalidOperationException("Please verify your email before logging in.");

            return BuildAuthResponse(parent.Id, email, parent.GetAttributeValue<string>("blser_name"));
        }

        public void ForgotPassword(ForgotPasswordRequest request)
        {
            var email = request?.Email?.Trim().ToLowerInvariant();
            var parent = FetchParentByEmail(email);
            if (parent == null) return; // deliberately silent -- see IParentService

            var otp = GenerateOtp();
            var otpMinutes = _config.GetSection("ParentAuth").GetValue<int?>("OtpExpiryMinutes") ?? 10;

            _org.Update(new Entity("blser_parent", parent.Id)
            {
                ["blser_otpcode"] = otp,
                ["blser_otpexpiresat"] = DateTime.UtcNow.AddMinutes(otpMinutes),
                ["blser_otppurpose"] = new OptionSetValue(OtpPurposeForgotPassword),
                ["blser_otpattempts"] = 0,
            });

            SendOtpEmail(email, parent.GetAttributeValue<string>("blser_name"), otp, "reset your password");
        }

        public void ResetPassword(ResetPasswordRequest request)
        {
            var email = request?.Email?.Trim().ToLowerInvariant();
            var parent = FetchParentByEmail(email) ?? throw new ArgumentException("Invalid request.");

            ValidateOtp(parent, request.OtpCode, OtpPurposeForgotPassword, "password reset");

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
                throw new ArgumentException("New password must be at least 8 characters.");

            _org.Update(new Entity("blser_parent", parent.Id)
            {
                ["blser_passwordhash"] = BCrypt.Net.BCrypt.HashPassword(request.NewPassword),
                ["blser_otpcode"] = null,
                ["blser_otpattempts"] = 0,
            });
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Entity FetchParentByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var fetch = $@"
<fetch top='1'>
  <entity name='blser_parent'>
    <attribute name='blser_name' />
    <attribute name='blser_email' />
    <attribute name='blser_phonenumber' />
    <attribute name='blser_passwordhash' />
    <attribute name='blser_emailverified' />
    <attribute name='blser_isactive' />
    <attribute name='blser_otpcode' />
    <attribute name='blser_otpexpiresat' />
    <attribute name='blser_otppurpose' />
    <attribute name='blser_otpattempts' />
    <filter>
      <condition attribute='blser_email' operator='eq' value='{email}' />
    </filter>
  </entity>
</fetch>";

            return _org.RetrieveMultiple(new FetchExpression(fetch)).Entities.FirstOrDefault();
        }

        private void ValidateOtp(Entity parent, string submittedCode, int expectedPurpose, string flowName)
        {
            if (string.IsNullOrWhiteSpace(submittedCode))
                throw new ArgumentException("OTP code is required.");

            var maxAttempts = _config.GetSection("ParentAuth").GetValue<int?>("MaxOtpAttempts") ?? 5;
            var attempts = parent.GetAttributeValue<int>("blser_otpattempts");
            var storedCode = parent.GetAttributeValue<string>("blser_otpcode");
            var expiresAt = parent.GetAttributeValue<DateTime?>("blser_otpexpiresat");
            var purpose = parent.GetAttributeValue<OptionSetValue>("blser_otppurpose")?.Value;

            var invalid = string.IsNullOrEmpty(storedCode)
                || purpose != expectedPurpose
                || expiresAt == null || expiresAt < DateTime.UtcNow
                || attempts >= maxAttempts
                || !string.Equals(storedCode, submittedCode.Trim(), StringComparison.Ordinal);

            if (invalid)
            {
                // Count the failed attempt so a code can't be brute-forced.
                _org.Update(new Entity("blser_parent", parent.Id)
                {
                    ["blser_otpattempts"] = attempts + 1,
                });

                throw new UnauthorizedAccessException(
                    $"That code is incorrect or has expired. Please request a new one for {flowName}.");
            }
        }

        private static string GenerateOtp()
        {
            // 6-digit numeric code, zero-padded (e.g. "004821").
            var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return value.ToString("D6");
        }

        private static int MapPurpose(string purpose) => purpose?.Trim().ToLower() switch
        {
            "registration" => OtpPurposeRegistration,
            "forgotpassword" => OtpPurposeForgotPassword,
            _ => throw new ArgumentException($"Unknown OTP purpose '{purpose}'. Expected 'Registration' or 'ForgotPassword'.")
        };

        private ParentAuthResponse BuildAuthResponse(Guid parentId, string email, string fullName)
        {
            var tokenResult = _tokens.GenerateParentToken(parentId, email, fullName);
            return new ParentAuthResponse
            {
                Token = tokenResult.Token,
                ExpiresAtUtc = tokenResult.ExpiresAtUtc,
                ParentId = parentId,
                FullName = fullName,
                Email = email,
            };
        }

        private void SendOtpEmail(string email, string fullName, string otp, string flowLabel)
        {
            var subject = "Your KiddoPay verification code";
            var body = $@"<p>Hi {System.Net.WebUtility.HtmlEncode(fullName)},</p>
<p>Use this code to {flowLabel}:</p>
<h2>{otp}</h2>
<p>This code expires in a few minutes. If you didn't request this, you can ignore this email.</p>";

            // IParentService's methods are synchronous (matches every other
            // service in this codebase -- Dataverse calls are synchronous
            // too), so the async email send is awaited inline here rather than
            // making every caller async. Safe under Kestrel (no
            // SynchronizationContext to deadlock against, unlike classic
            // ASP.NET). Deliberately not fire-and-forget: if sending fails,
            // the caller (Register/ResendOtp/ForgotPassword) should know
            // rather than silently leaving the parent without a code.
            _email.SendEmailAsync(email, subject, body).GetAwaiter().GetResult();
        }
    }
}
