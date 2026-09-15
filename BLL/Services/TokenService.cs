using KiddoPay.BLL.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace KiddoPay.BLL.Services
{
    public class TokenService(IConfiguration configuration) : ITokenService
    {
        private readonly IConfiguration _config = configuration;

        public ParentTokenResult GenerateParentToken(Guid parentId, string email, string fullName)
        {
            var section = _config.GetSection("ParentAuth");
            var signingKey = section["JwtSigningKey"]
                ?? throw new InvalidOperationException("ParentAuth:JwtSigningKey is not configured.");
            var issuer = section["Issuer"];
            var audience = section["Audience"];
            var minutes = section.GetValue<int?>("AccessTokenMinutes") ?? 1440; // 24h default

            var expiresAtUtc = DateTime.UtcNow.AddMinutes(minutes);

            // ClaimTypes.NameIdentifier carries the blser_parent GUID directly --
            // controllers read it straight off User (see DeviceTokensController),
            // no lookup-by-external-id step needed like the cashier/Azure-AD flow.
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, parentId.ToString()),
                new Claim(ClaimTypes.Email, email ?? ""),
                new Claim(ClaimTypes.Name, fullName ?? ""),
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: credentials);

            return new ParentTokenResult
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAtUtc = expiresAtUtc,
            };
        }
    }
}
