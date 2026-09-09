using System.IdentityModel.Tokens.Jwt;
using MeetingRoomBooking.Api.Modules.Auth.Jwt;
using Xunit;

namespace Backend.UnitTests;

/// <summary>
/// ADR 0001: the access token "contains only the stable subject identifier and required
/// role claims; secrets and personal profile data are excluded." This pins that contract
/// at the token level, independent of the HTTP layer.
/// </summary>
public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        SigningKey = "unit-test-signing-key-at-least-32-chars-long",
        Issuer = "MeetingRoomBooking.Api.Tests",
        Audience = "MeetingRoomBooking.Client.Tests",
        AccessTokenLifetimeMinutes = 15,
    };

    [Fact]
    public void CreateAccessToken_ContainsOnlySubjectAndRoleClaims()
    {
        var service = new JwtTokenService(Options);
        var userId = Guid.NewGuid();

        var (accessToken, _) = service.CreateAccessToken(userId, ["User", "Admin"]);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        Assert.Equal(userId.ToString(), token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(["User", "Admin"], token.Claims.Where(c => c.Type == JwtTokenService.RoleClaimType).Select(c => c.Value));

        var allowedClaimTypes = new[]
        {
            JwtRegisteredClaimNames.Sub, JwtTokenService.RoleClaimType,
            JwtRegisteredClaimNames.Nbf, JwtRegisteredClaimNames.Exp,
            JwtRegisteredClaimNames.Iss, JwtRegisteredClaimNames.Aud,
        };
        Assert.All(token.Claims, claim => Assert.Contains(claim.Type, allowedClaimTypes));
    }

    [Fact]
    public void CreateAccessToken_ExpiresAfterConfiguredLifetime()
    {
        var service = new JwtTokenService(Options);

        var (_, expiresAtUtc) = service.CreateAccessToken(Guid.NewGuid(), []);

        var expectedExpiry = DateTime.UtcNow.AddMinutes(Options.AccessTokenLifetimeMinutes);
        Assert.True(Math.Abs((expiresAtUtc - expectedExpiry).TotalSeconds) < 5);
    }

    [Fact]
    public void CreateAccessToken_UsesConfiguredIssuerAndAudience()
    {
        var service = new JwtTokenService(Options);

        var (accessToken, _) = service.CreateAccessToken(Guid.NewGuid(), []);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        Assert.Equal(Options.Issuer, token.Issuer);
        Assert.Equal(Options.Audience, Assert.Single(token.Audiences));
    }
}
