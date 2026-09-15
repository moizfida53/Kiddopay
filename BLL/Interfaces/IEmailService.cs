namespace Kiddopay.BLL.Interfaces
{
    /// <summary>
    /// Sends transactional email. The registered implementation is currently
    /// DevLoggingEmailService, a placeholder that logs the email instead of
    /// actually sending it -- swap in a real provider (SendGrid, Azure
    /// Communication Services, SMTP, etc.) by registering a different
    /// IEmailService implementation in Program.cs once one is chosen. Nothing
    /// else in the codebase needs to change when that happens.
    /// </summary>
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    }
}
