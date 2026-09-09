using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MeetingRoomBooking.Api.Modules.Auth.Jwt;

/// <summary>
/// Issues short-lived access tokens carrying only the stable subject identifier and role
/// claims, per ADR 0001 — no personal profile data or secrets.
/// </summary>
public sealed class JwtTokenService(JwtOptions options)
{
    public const string RoleClaimType = "role";

    public (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(Guid userId, IEnumerable<string> roles)
    {
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(options.AccessTokenLifetimeMinutes);

        List<Claim> claims = [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())];
        claims.AddRange(roles.Select(role => new Claim(RoleClaimType, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
