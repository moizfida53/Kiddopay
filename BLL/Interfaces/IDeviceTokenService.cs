using Kiddopay.BLL.DTOs;

namespace Kiddopay.BLL.Interfaces
{
    public interface IDeviceTokenService
    {
        /// <summary>
        /// Registers (or refreshes) a parent's FCM device token. Upserts by
        /// token value -- FCM tokens can be reused across reinstalls/re-logins
        /// on the same device, and a token is only ever meaningfully tied to
        /// one parent at a time, so token is the safe uniqueness key here
        /// rather than (parent, token) pairs.
        /// </summary>
        void RegisterToken(Guid parentId, RegisterDeviceTokenRequest request);

        /// <summary>
        /// Returns every active device token on file for a parent -- a parent
        /// may have the app installed on more than one device.
        /// </summary>
        List<string> GetTokensForParent(Guid parentId);

        /// <summary>
        /// Deactivates a token after FCM reports it as no longer registered
        /// (app uninstalled, token expired/rotated, etc.), so
        /// NotificationService stops sending to it on future alerts.
        /// </summary>
        void DeactivateToken(string token);
    }
}
