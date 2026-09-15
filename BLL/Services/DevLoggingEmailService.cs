using Kiddopay.BLL.Interfaces;

namespace KiddoPay.BLL.Services
{
    /// <summary>
    /// PLACEHOLDER -- logs the email instead of sending it. This lets the whole
    /// registration/login/OTP/forgot-password flow be built and tested end to
    /// end (the OTP shows up in the backend console/log output) before a real
    /// email provider is chosen and wired in. Replace the DI registration in
    /// Program.cs with a real IEmailService implementation (SendGrid, Azure
    /// Communication Services, SMTP, etc.) when ready -- nothing else needs to
    /// change, every caller only depends on IEmailService.
    /// </summary>
    public class DevLoggingEmailService(ILogger<DevLoggingEmailService> logger) : IEmailService
    {
        private readonly ILogger<DevLoggingEmailService> _logger = logger;

        public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            _logger.LogWarning(
                "[DEV EMAIL -- not actually sent] To: {ToEmail} | Subject: {Subject}\n{Body}",
                toEmail, subject, htmlBody);
            return Task.CompletedTask;
        }
    }
}
