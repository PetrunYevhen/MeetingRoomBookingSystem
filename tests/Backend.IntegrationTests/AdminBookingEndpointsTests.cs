using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>
/// Covers the task's "Admin ... can view all bookings across users" requirement: the
/// admin list must span users (not just the caller's own bookings) and must stay closed
/// to the `User` role.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminBookingEndpointsTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminBookingEndpointsTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListAllBookings_AsAdmin_IncludesBookingsFromEveryUser()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, resourceName) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var firstSlot = await _client.CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));
        var secondSlot = await _client.CreateSlotAsAdminAsync(
            adminToken, resourceId, start.AddHours(2), start.AddHours(3));

        var firstUser = await _client.RegisterAndLoginUserAsync();
        var secondUser = await _client.RegisterAndLoginUserAsync();
        await BookAsync(firstUser, firstSlot.Id);
        await BookAsync(secondUser, secondSlot.Id);

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Get, $"/api/v1/admin/bookings?resourceId={resourceId}", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bookings = await response.Content.ReadFromJsonAsync<List<AdminBookingDto>>();

        Assert.Equal(2, bookings!.Count);
        Assert.All(bookings, booking =>
        {
            Assert.Equal(resourceId, booking.ResourceId);
            Assert.Equal(resourceName, booking.ResourceName);
            Assert.EndsWith("@example.com", booking.UserEmail);
        });

        // Two different users, so the endpoint is genuinely cross-user rather than
        // "every booking that happens to belong to whoever is calling".
        Assert.Equal(2, bookings.Select(booking => booking.UserId).Distinct().Count());

        // Ordered by slot start, not insertion order.
        Assert.Equal(firstSlot.Id, bookings[0].TimeSlotId);
        Assert.Equal(secondSlot.Id, bookings[1].TimeSlotId);
    }

    [Fact]
    public async Task ListAllBookings_WithPaging_ReturnsOnlyTheRequestedWindow()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(2);

        var slots = new List<TimeSlotDto>();
        for (var index = 0; index < 3; index++)
        {
            var slotStart = start.AddHours(index * 2);
            slots.Add(await _client.CreateSlotAsAdminAsync(adminToken, resourceId, slotStart, slotStart.AddHours(1)));
            await BookAsync(await _client.RegisterAndLoginUserAsync(), slots[index].Id);
        }

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Get, $"/api/v1/admin/bookings?resourceId={resourceId}&skip=1&take=1", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<List<AdminBookingDto>>();

        Assert.Single(page!);
        Assert.Equal(slots[1].Id, page![0].TimeSlotId);
    }

    [Fact]
    public async Task ListAllBookings_AsUser_ReturnsForbidden()
    {
        var userToken = await _client.RegisterAndLoginUserAsync();

        var response = await _client.SendAuthorizedAsync(HttpMethod.Get, "/api/v1/admin/bookings", userToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListAllBookings_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/admin/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task BookAsync(string userToken, Guid timeSlotId)
    {
        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Post, "/api/v1/bookings", userToken, new { timeSlotId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<string> AdminTokenAsync() =>
        await _client.LoginAsAdminAsync(_factory.Services.GetRequiredService<IConfiguration>());
}
