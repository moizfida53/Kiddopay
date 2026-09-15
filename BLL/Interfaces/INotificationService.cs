namespace Kiddopay.BLL.Interfaces
{
    public interface INotificationService
    {
        /// <summary>
        /// Sends a push notification to every device registered to the given
        /// parent. Safe to call unconditionally -- it no-ops (and logs) when
        /// Firebase isn't configured yet, or when the parent has no registered
        /// devices, so callers never need to guard this themselves.
        /// </summary>
        Task SendToParentAsync(Guid parentId, string title, string body, Dictionary<string, string>? data = null);
    }
}
