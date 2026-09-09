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
/// (Testcontainers). `SampleDataSeeder` already seeds Falcon/Phoenix, so assertions use
/// generated names/Contains rather than exact counts. "Booked" scenarios insert a
/// `Booking` row directly through `ApplicationDbContext` to keep these tests independent
/// of the booking endpoint, the same technique `DatabaseSchemaTests` already uses.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ResourceEndpointsTests : IClassFixture<ApiApplicationFactory>
{
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
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(await AdminTokenAsync());
        var userToken = await _client.RegisterAndLoginUserAsync();

        var response = await _client.SendAuthorizedAsync(HttpMethod.Get, "/api/v1/resources", userToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resources = await response.Content.ReadFromJsonAsync<List<ResourceDto>>();
        Assert.Contains(resources!, r => r.Id == resourceId);
    }

    [Fact]
    public async Task GetResource_NotFound_Returns404()
    {
        var userToken = await _client.RegisterAndLoginUserAsync();

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Get, $"/api/v1/resources/{Guid.NewGuid()}", userToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateResource_AsUser_ReturnsForbidden()
    {
        var userToken = await _client.RegisterAndLoginUserAsync();

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Post, "/api/v1/admin/resources", userToken, new { name = ApiTestHelpers.NewName("Room") });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetResourceSlots_ReportsBookedAndAvailableStatus_AndFiltersByDateRange()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);

        var soon = DateTime.UtcNow.AddDays(1);
        var far = DateTime.UtcNow.AddDays(10);
        var earlySlot = await _client.CreateSlotAsAdminAsync(adminToken, resourceId, soon, soon.AddHours(1));
        var lateSlot = await _client.CreateSlotAsAdminAsync(adminToken, resourceId, far, far.AddHours(1));

        await InsertBookingDirectlyAsync(earlySlot.Id);

        var userToken = await _client.RegisterAndLoginUserAsync();
        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Get, $"/api/v1/resources/{resourceId}/slots", userToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var slots = await response.Content.ReadFromJsonAsync<List<TimeSlotDto>>();
        Assert.NotNull(slots);
        Assert.Equal("booked", slots.Single(s => s.Id == earlySlot.Id).Status);
        Assert.Equal("available", slots.Single(s => s.Id == lateSlot.Id).Status);

        var fromUtc = Uri.EscapeDataString(DateTime.UtcNow.AddDays(5).ToString("O"));
        var filtered = await _client.SendAuthorizedAsync(
            HttpMethod.Get, $"/api/v1/resources/{resourceId}/slots?fromUtc={fromUtc}", userToken);
        var filteredSlots = await filtered.Content.ReadFromJsonAsync<List<TimeSlotDto>>();
        Assert.DoesNotContain(filteredSlots!, s => s.Id == earlySlot.Id);
        Assert.Contains(filteredSlots!, s => s.Id == lateSlot.Id);
    }

    [Fact]
    public async Task UpdateResource_ReplacesName()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var newName = ApiTestHelpers.NewName("Renamed");

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Put, $"/api/v1/admin/resources/{resourceId}", adminToken, new { name = newName });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ResourceDto>();
        Assert.Equal(newName, updated!.Name);
    }

    [Fact]
    public async Task DeleteResource_WithoutSlots_ReturnsNoContent()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Delete, $"/api/v1/admin/resources/{resourceId}", adminToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteResource_WithSlots_ReturnsConflict()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        await _client.CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Delete, $"/api/v1/admin/resources/{resourceId}", adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("resource_has_slots", problem!.Code);
    }

    [Fact]
    public async Task CreateSlot_InvalidRange_ReturnsBadRequest()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Post, $"/api/v1/admin/resources/{resourceId}/slots", adminToken,
            new { startUtc = start, endUtc = start });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("validation_failed", problem!.Code);
    }

    [Fact]
    public async Task CreateSlot_DuplicateRange_ReturnsConflict()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(1);
        await _client.CreateSlotAsAdminAsync(adminToken, resourceId, start, end);

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Post, $"/api/v1/admin/resources/{resourceId}/slots", adminToken,
            new { startUtc = start, endUtc = end });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("duplicate_slot", problem!.Code);
    }

    [Fact]
    public async Task UpdateSlot_OnBookedSlot_ReturnsConflict()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var slot = await _client.CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));
        await InsertBookingDirectlyAsync(slot.Id);

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Put, $"/api/v1/admin/slots/{slot.Id}", adminToken,
            new { startUtc = start.AddHours(2), endUtc = start.AddHours(3) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("timeslot_has_booking", problem!.Code);
    }

    [Fact]
    public async Task DeleteSlot_OnBookedSlot_ReturnsConflict()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var slot = await _client.CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));
        await InsertBookingDirectlyAsync(slot.Id);

        var response = await _client.SendAuthorizedAsync(HttpMethod.Delete, $"/api/v1/admin/slots/{slot.Id}", adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("timeslot_has_booking", problem!.Code);
    }

    [Fact]
    public async Task DeleteSlot_Unbooked_ReturnsNoContent()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var slot = await _client.CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));

        var response = await _client.SendAuthorizedAsync(HttpMethod.Delete, $"/api/v1/admin/slots/{slot.Id}", adminToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetResourceSlots_SerializesTimestampsAsExplicitUtc()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = new DateTime(2030, 5, 4, 9, 0, 0, DateTimeKind.Utc);
        await _client.CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Get, $"/api/v1/resources/{resourceId}/slots", adminToken);
        var payload = await response.Content.ReadAsStringAsync();

        // Without the offset a client is free to read a UTC instant as local wall-clock
        // time — exactly what the SPA's `new Date(...)` does — so the `Z` is part of the
        // contract, not cosmetic (see `ApplicationDbContext`'s UTC value converter).
        Assert.Contains("\"startUtc\":\"2030-05-04T09:00:00Z\"", payload);
        Assert.Contains("\"endUtc\":\"2030-05-04T10:00:00Z\"", payload);
    }

    private async Task<string> AdminTokenAsync() =>
        await _client.LoginAsAdminAsync(_factory.Services.GetRequiredService<IConfiguration>());

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
}
