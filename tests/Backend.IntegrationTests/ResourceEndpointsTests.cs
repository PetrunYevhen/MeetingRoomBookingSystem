using System.Net;
using System.Net.Http.Json;
using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Bookings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>
/// End-to-end proof of ADR 0001's Resources read/admin surface against a real SQL Server
/// (Testcontainers). `DevelopmentDataSeeder` already seeds Falcon/Phoenix in this
/// environment, so assertions use generated names/Contains rather than exact counts.
/// No booking endpoint exists yet, so "booked" scenarios insert a `Booking` row directly
/// through `ApplicationDbContext`, same technique `DatabaseSchemaTests` already uses.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ResourceEndpointsTests : IClassFixture<ApiApplicationFactory>
{
    private const string Password = "Str0ngP@ssword1";

    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public ResourceEndpointsTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListResources_AsUser_ContainsCreatedResource()
    {
        var (resourceId, _) = await CreateResourceAsAdminAsync();
        var userToken = await RegisterAndLoginUserAsync();

        var response = await SendAsync(HttpMethod.Get, "/api/v1/resources", userToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resources = await response.Content.ReadFromJsonAsync<List<ResourceDto>>();
        Assert.Contains(resources!, r => r.Id == resourceId);
    }

    [Fact]
    public async Task GetResource_NotFound_Returns404()
    {
        var userToken = await RegisterAndLoginUserAsync();

        var response = await SendAsync(HttpMethod.Get, $"/api/v1/resources/{Guid.NewGuid()}", userToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateResource_AsUser_ReturnsForbidden()
    {
        var userToken = await RegisterAndLoginUserAsync();

        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/resources", userToken, new { name = NewName("Room") });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetResourceSlots_ReportsBookedAndAvailableStatus_AndFiltersByDateRange()
    {
        var adminToken = await LoginAsAdminAsync();
        var (resourceId, _) = await CreateResourceAsAdminAsync(adminToken);

        var soon = DateTime.UtcNow.AddDays(1);
        var far = DateTime.UtcNow.AddDays(10);
        var earlySlot = await CreateSlotAsAdminAsync(adminToken, resourceId, soon, soon.AddHours(1));
        var lateSlot = await CreateSlotAsAdminAsync(adminToken, resourceId, far, far.AddHours(1));

        await InsertBookingDirectlyAsync(earlySlot.Id);

        var userToken = await RegisterAndLoginUserAsync();
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/resources/{resourceId}/slots", userToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var slots = await response.Content.ReadFromJsonAsync<List<TimeSlotDto>>();
        Assert.NotNull(slots);
        Assert.Equal("booked", slots.Single(s => s.Id == earlySlot.Id).Status);
        Assert.Equal("available", slots.Single(s => s.Id == lateSlot.Id).Status);

        var fromUtc = Uri.EscapeDataString(DateTime.UtcNow.AddDays(5).ToString("O"));
        var filtered = await SendAsync(
            HttpMethod.Get, $"/api/v1/resources/{resourceId}/slots?fromUtc={fromUtc}", userToken);
        var filteredSlots = await filtered.Content.ReadFromJsonAsync<List<TimeSlotDto>>();
        Assert.DoesNotContain(filteredSlots!, s => s.Id == earlySlot.Id);
        Assert.Contains(filteredSlots!, s => s.Id == lateSlot.Id);
    }

    [Fact]
    public async Task UpdateResource_ReplacesName()
    {
        var adminToken = await LoginAsAdminAsync();
        var (resourceId, _) = await CreateResourceAsAdminAsync(adminToken);
        var newName = NewName("Renamed");

        var response = await SendAsync(
            HttpMethod.Put, $"/api/v1/admin/resources/{resourceId}", adminToken, new { name = newName });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ResourceDto>();
        Assert.Equal(newName, updated!.Name);
    }

    [Fact]
    public async Task DeleteResource_WithoutSlots_ReturnsNoContent()
    {
        var adminToken = await LoginAsAdminAsync();
        var (resourceId, _) = await CreateResourceAsAdminAsync(adminToken);

        var response = await SendAsync(HttpMethod.Delete, $"/api/v1/admin/resources/{resourceId}", adminToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteResource_WithSlots_ReturnsConflict()
    {
        var adminToken = await LoginAsAdminAsync();
        var (resourceId, _) = await CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        await CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));

        var response = await SendAsync(HttpMethod.Delete, $"/api/v1/admin/resources/{resourceId}", adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("resource_has_slots", problem!.Code);
    }

    [Fact]
    public async Task CreateSlot_InvalidRange_ReturnsBadRequest()
    {
        var adminToken = await LoginAsAdminAsync();
        var (resourceId, _) = await CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/resources/{resourceId}/slots", adminToken,
            new { startUtc = start, endUtc = start });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("validation_failed", problem!.Code);
    }

    [Fact]
    public async Task CreateSlot_DuplicateRange_ReturnsConflict()
    {
        var adminToken = await LoginAsAdminAsync();
        var (resourceId, _) = await CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(1);
        await CreateSlotAsAdminAsync(adminToken, resourceId, start, end);

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/resources/{resourceId}/slots", adminToken,
            new { startUtc = start, endUtc = end });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("duplicate_slot", problem!.Code);
    }

    [Fact]
    public async Task UpdateSlot_OnBookedSlot_ReturnsConflict()
    {
        var adminToken = await LoginAsAdminAsync();
        var (resourceId, _) = await CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var slot = await CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));
        await InsertBookingDirectlyAsync(slot.Id);

        var response = await SendAsync(
            HttpMethod.Put, $"/api/v1/admin/slots/{slot.Id}", adminToken,
            new { startUtc = start.AddHours(2), endUtc = start.AddHours(3) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("timeslot_has_booking", problem!.Code);
    }

    [Fact]
    public async Task DeleteSlot_OnBookedSlot_ReturnsConflict()
    {
        var adminToken = await LoginAsAdminAsync();
        var (resourceId, _) = await CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var slot = await CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));
        await InsertBookingDirectlyAsync(slot.Id);

        var response = await SendAsync(HttpMethod.Delete, $"/api/v1/admin/slots/{slot.Id}", adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("timeslot_has_booking", problem!.Code);
    }

    [Fact]
    public async Task DeleteSlot_Unbooked_ReturnsNoContent()
    {
        var adminToken = await LoginAsAdminAsync();
        var (resourceId, _) = await CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var slot = await CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));

        var response = await SendAsync(HttpMethod.Delete, $"/api/v1/admin/slots/{slot.Id}", adminToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<string> LoginAsAdminAsync()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var email = configuration["Admin:Email"]!;
        var password = configuration["Admin:Password"]!;

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return body!.AccessToken;
    }

    private async Task<string> RegisterAndLoginUserAsync()
    {
        var email = $"resource-test-{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return body!.AccessToken;
    }

    private async Task<(Guid Id, string Name)> CreateResourceAsAdminAsync(string? adminToken = null)
    {
        adminToken ??= await LoginAsAdminAsync();
        var name = NewName("Room");
        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/resources", adminToken, new { name });
        var created = await response.Content.ReadFromJsonAsync<ResourceDto>();
        return (created!.Id, created.Name);
    }

    private async Task<TimeSlotDto> CreateSlotAsAdminAsync(
        string adminToken, Guid resourceId, DateTime startUtc, DateTime endUtc)
    {
        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/resources/{resourceId}/slots", adminToken, new { startUtc, endUtc });
        return (await response.Content.ReadFromJsonAsync<TimeSlotDto>())!;
    }

    private async Task InsertBookingDirectlyAsync(Guid timeSlotId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"booking-owner-{Guid.NewGuid()}@example.com",
            Email = $"booking-owner-{Guid.NewGuid()}@example.com",
        };
        dbContext.Users.Add(owner);
        dbContext.Bookings.Add(new Booking { Id = Guid.NewGuid(), TimeSlotId = timeSlotId, UserId = owner.Id });
        await dbContext.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string accessToken, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    private static string NewName(string prefix) => $"{prefix}-{Guid.NewGuid()}";

    private sealed record ResourceDto(Guid Id, string Name);

    private sealed record TimeSlotDto(Guid Id, DateTime StartUtc, DateTime EndUtc, string Status);

    private sealed record UserProfileDto(Guid Id, string Email, string[] Roles);

    private sealed record AuthResponseDto(string AccessToken, DateTime AccessTokenExpiresAtUtc, UserProfileDto User);

    private sealed record ProblemDetailsDto(string Type, string Title, int Status, string? Detail, string? Instance, string Code);
}
