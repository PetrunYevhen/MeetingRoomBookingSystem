using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Backend.IntegrationTests;

internal sealed record ResourceDto(Guid Id, string Name);

internal sealed record TimeSlotDto(Guid Id, DateTime StartUtc, DateTime EndUtc, string Status);

internal sealed record AdminBookingDto(
    Guid Id,
    Guid ResourceId,
    string ResourceName,
    Guid TimeSlotId,
    DateTime StartUtc,
    DateTime EndUtc,
    Guid UserId,
    string UserEmail,
    DateTime CreatedAtUtc);

internal sealed record UserProfileDto(Guid Id, string Email, string[] Roles);

internal sealed record AuthResponseDto(string AccessToken, DateTime AccessTokenExpiresAtUtc, UserProfileDto User);

internal sealed record ProblemDetailsDto(string Type, string Title, int Status, string? Detail, string? Instance, string Code);

/// <summary>
/// Shared across integration test classes: bearer-token auth flows and admin-created
/// fixtures every module's tests need (register/login, seeded-admin login, room/slot
/// creation). Auth-specific concerns like the refresh cookie stay in
/// <c>AuthEndpointsTests</c> — those aren't needed outside that one test class.
/// </summary>
internal static class ApiTestHelpers
{
    private const string DefaultPassword = "Str0ngP@ssword1";

    public static string NewEmail(string prefix = "test-user") => $"{prefix}-{Guid.NewGuid()}@example.com";

    public static string NewName(string prefix) => $"{prefix}-{Guid.NewGuid()}";

    public static async Task<string> LoginAsAdminAsync(this HttpClient client, IConfiguration configuration)
    {
        var email = configuration["Admin:Email"]!;
        var password = configuration["Admin:Password"]!;

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return body!.AccessToken;
    }

    public static async Task<string> RegisterAndLoginUserAsync(this HttpClient client)
    {
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = DefaultPassword });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = DefaultPassword });
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return body!.AccessToken;
    }

    public static async Task<(Guid Id, string Name)> CreateResourceAsAdminAsync(
        this HttpClient client, string adminToken, string? name = null)
    {
        var response = await client.SendAuthorizedAsync(
            HttpMethod.Post, "/api/v1/admin/resources", adminToken, new { name = name ?? NewName("Room") });
        var created = await response.Content.ReadFromJsonAsync<ResourceDto>();
        return (created!.Id, created.Name);
    }

    public static async Task<TimeSlotDto> CreateSlotAsAdminAsync(
        this HttpClient client, string adminToken, Guid resourceId, DateTime startUtc, DateTime endUtc)
    {
        var response = await client.SendAuthorizedAsync(
            HttpMethod.Post, $"/api/v1/admin/resources/{resourceId}/slots", adminToken, new { startUtc, endUtc });
        return (await response.Content.ReadFromJsonAsync<TimeSlotDto>())!;
    }

    public static async Task<HttpResponseMessage> SendAuthorizedAsync(
        this HttpClient client, HttpMethod method, string url, string accessToken, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }
}
