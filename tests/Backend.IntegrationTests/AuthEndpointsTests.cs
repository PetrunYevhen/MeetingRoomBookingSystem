using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>
/// End-to-end proof of ADR 0001's Auth surface against a real SQL Server (Testcontainers):
/// register/login/refresh/logout/me, the origin check on cookie-authenticated routes, and
/// refresh-token reuse detection revoking a whole rotation family.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuthEndpointsTests : IClassFixture<ApiApplicationFactory>
{
    private const string AllowedOrigin = "http://localhost:5173";
    private const string Password = "Str0ngP@ssword1";

    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    [Fact]
    public async Task Register_ReturnsCreated_WithUserRoleOnly()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = NewEmail(),
            password = Password,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal(["User"], profile.Roles);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var email = NewEmail();
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("email_already_registered", problem!.Code);
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register", new { email = NewEmail(), password = "weak" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("registration_failed", problem!.Code);
    }

    [Fact]
    public async Task Login_WrongPasswordAndNonexistentEmail_ReturnSameGenericProblem()
    {
        var email = NewEmail();
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });

        var wrongPassword = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = "SomethingElse1!" });
        var nonexistentEmail = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = NewEmail(), password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, nonexistentEmail.StatusCode);

        var wrongPasswordProblem = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        var nonexistentEmailProblem = await nonexistentEmail.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal(wrongPasswordProblem!.Code, nonexistentEmailProblem!.Code);
        Assert.Equal(wrongPasswordProblem.Detail, nonexistentEmailProblem.Detail);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsProfile()
    {
        var email = NewEmail();
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });
        var login = await LoginAsync(email);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("Authorization", $"Bearer {login.AccessToken}");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Equal(email, profile!.Email);
    }

    [Fact]
    public async Task Refresh_ValidCookieAndOrigin_RotatesTokenAndCookie()
    {
        var email = NewEmail();
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });
        var login = await LoginAsync(email);

        var response = await RefreshAsync(login.Cookie, AllowedOrigin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));

        // The access token itself can be byte-identical to the one from login (same user,
        // same roles, same second — no jti/nonce claim per ADR's minimal-claims rule). The
        // refresh token is what must have rotated: a fresh opaque value, distinct cookie.
        var newCookie = ExtractCookiePair(response);
        Assert.NotEqual(login.Cookie, newCookie);
    }

    [Fact]
    public async Task Refresh_MissingOrAllowlistedOrigin_ReturnsForbidden()
    {
        var email = NewEmail();
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });
        var login = await LoginAsync(email);

        var missingOrigin = await RefreshAsync(login.Cookie, origin: null);
        var wrongOrigin = await RefreshAsync(login.Cookie, "https://evil.example.com");

        Assert.Equal(HttpStatusCode.Forbidden, missingOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongOrigin.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReusedRevokedToken_RevokesWholeFamily()
    {
        var email = NewEmail();
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });
        var login = await LoginAsync(email);

        var firstRotation = await RefreshAsync(login.Cookie, AllowedOrigin);
        var rotatedCookie = ExtractCookiePair(firstRotation);

        // Replay the original, now-revoked cookie: reuse detected.
        var replay = await RefreshAsync(login.Cookie, AllowedOrigin);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The token from the successful rotation is now also dead: the whole family was revoked.
        var latestAttempt = await RefreshAsync(rotatedCookie, AllowedOrigin);
        Assert.Equal(HttpStatusCode.Unauthorized, latestAttempt.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesSession_SubsequentRefreshFails()
    {
        var email = NewEmail();
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });
        var login = await LoginAsync(email);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Add("Cookie", login.Cookie);
        logoutRequest.Headers.Add("Origin", AllowedOrigin);
        var logoutResponse = await _client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshAfterLogout = await RefreshAsync(login.Cookie, AllowedOrigin);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_ReturnsAdminRole()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var adminEmail = configuration["Admin:Email"]!;
        var adminPassword = configuration["Admin:Password"]!;

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = adminEmail, password = adminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.Equal(["Admin"], body!.User.Roles);
    }

    private async Task<(string AccessToken, string Cookie)> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return (body!.AccessToken, ExtractCookiePair(response));
    }

    private async Task<HttpResponseMessage> RefreshAsync(string cookie, string? origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await _client.SendAsync(request);
    }

    private static string ExtractCookiePair(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

    private static string NewEmail() => $"user-{Guid.NewGuid()}@example.com";

    private sealed record UserProfileDto(Guid Id, string Email, string[] Roles);

    private sealed record AuthResponseDto(string AccessToken, DateTime AccessTokenExpiresAtUtc, UserProfileDto User);

    private sealed record ProblemDetailsDto(string Type, string Title, int Status, string? Detail, string? Instance, string Code);
}
