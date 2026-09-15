namespace Kiddopay.BLL.Interfaces
{
    public class ParentTokenResult
    {
        public string Token { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    /// <summary>
    /// Issues the backend's own JWTs for the parent mobile app. Parents don't
    /// use Microsoft/Azure AD login (that's only for the cashier web app), so
    /// there's no external identity provider to lean on here -- this backend
    /// is the identity provider for parents, using the "ParentScheme" JWT
    /// bearer scheme registered in Program.cs.
    /// </summary>
    public interface ITokenService
    {
        ParentTokenResult GenerateParentToken(Guid parentId, string email, string fullName);
    }
}
