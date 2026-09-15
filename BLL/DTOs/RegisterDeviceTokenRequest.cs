namespace Kiddopay.BLL.DTOs
{
    // ─── Device Tokens (push notifications) ──────────────────────────────────

    public class RegisterDeviceTokenRequest
    {
        // ParentId removed on purpose -- it now comes from the caller's own
        // JWT (ParentScheme), not from the request body. See
        // DeviceTokensController.RegisterToken.
        public string Token { get; set; }
        public string Platform { get; set; }   // "Android" | "iOS"
    }
}
