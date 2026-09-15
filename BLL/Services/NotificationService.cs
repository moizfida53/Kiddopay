using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Kiddopay.BLL.Interfaces;

namespace KiddoPay.BLL.Services
{
    public class NotificationService(IDeviceTokenService deviceTokenService, ILogger<NotificationService> logger) : INotificationService
    {
        private readonly IDeviceTokenService _deviceTokens = deviceTokenService;
        private readonly ILogger<NotificationService> _logger = logger;

        public async Task SendToParentAsync(Guid parentId, string title, string body, Dictionary<string, string>? data = null)
        {
            // FirebaseApp.DefaultInstance is null until Program.cs successfully
            // finds and loads the service-account key (see the setup guide) --
            // treat "not configured yet" as a no-op rather than a crash, so the
            // rest of the app keeps working before/while that's being set up.
            if (FirebaseApp.DefaultInstance == null)
            {
                _logger.LogWarning("Firebase is not configured -- skipping push notification '{Title}' to parent {ParentId}.", title, parentId);
                return;
            }

            var tokens = _deviceTokens.GetTokensForParent(parentId);
            if (tokens.Count == 0)
            {
                _logger.LogInformation("Parent {ParentId} has no registered devices -- skipping push notification '{Title}'.", parentId, title);
                return;
            }

            foreach (var token in tokens)
            {
                var message = new Message
                {
                    Token = token,
                    Notification = new Notification { Title = title, Body = body },
                    Data = data,
                };

                try
                {
                    await FirebaseMessaging.DefaultInstance.SendAsync(message);
                }
                catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
                {
                    // The app was uninstalled, or the token expired/rotated --
                    // this is the expected way FCM tells us a token is dead.
                    // Deactivate it so we stop retrying it on every future alert.
                    _logger.LogInformation("Device token for parent {ParentId} is no longer registered -- deactivating.", parentId);
                    _deviceTokens.DeactivateToken(token);
                }
                catch (FirebaseMessagingException ex)
                {
                    // Any other FCM-side failure (rate limit, malformed message,
                    // etc.) -- log and move on to the next token rather than
                    // letting one bad send block the rest of this parent's
                    // devices, or bubble up into the caller's own flow (e.g. an
                    // order-completion request should still succeed even if the
                    // notification about it fails to send).
                    _logger.LogError(ex, "Failed to send push notification '{Title}' to parent {ParentId}.", title, parentId);
                }
            }
        }
    }
}
