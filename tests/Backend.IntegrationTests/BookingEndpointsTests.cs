using System.Net;
using System.Net.Http.Json;
using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Modules.Bookings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>
/// The centerpiece test of the whole task (ADR 0002's own "Verification strategy",
/// executed almost verbatim): fire many genuinely concurrent `POST /api/v1/bookings`
/// requests at the same slot and prove exactly one wins. The unique index
/// `UX_Bookings_TimeSlotId` — not this test, not application code — is what makes that
/// true; this only proves the guarantee end-to-end over real HTTP against a real SQL
/// Server (Testcontainers).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BookingEndpointsTests : IClassFixture<BookingApiApplicationFactory>
{
    private readonly BookingApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public BookingEndpointsTests(BookingApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateBooking_ValidSlot_ReturnsCreated()
    {
        var (userToken, slot) = await CreateUserAndFreshSlotAsync();

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Post, "/api/v1/bookings", userToken, new { timeSlotId = slot.Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_NonexistentSlot_ReturnsNotFound()
    {
        var userToken = await _client.RegisterAndLoginUserAsync();

        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Post, "/api/v1/bookings", userToken, new { timeSlotId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/bookings", new { timeSlotId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_SecondAttemptSameSlot_ReturnsExactConflictBodyFromAdr0002()
    {
        var (userToken, slot) = await CreateUserAndFreshSlotAsync();
        await _client.SendAuthorizedAsync(HttpMethod.Post, "/api/v1/bookings", userToken, new { timeSlotId = slot.Id });

        var secondUserToken = await _client.RegisterAndLoginUserAsync();
        var response = await _client.SendAuthorizedAsync(
            HttpMethod.Post, "/api/v1/bookings", secondUserToken, new { timeSlotId = slot.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("/problems/slot-already-booked", problem!.Type);
        Assert.Equal("The time slot is already booked.", problem.Title);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Another booking was committed for the requested time slot.", problem.Detail);
        Assert.Equal("/api/v1/bookings", problem.Instance);
        Assert.Equal("slot_already_booked", problem.Code);
    }

    [Fact]
    public async Task CreateBooking_ConcurrentRequestsForSameSlot_ExactlyOneSucceeds()
    {
        const int concurrentRequests = 15;

        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var slot = await _client.CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));

        var userTokens = await Task.WhenAll(
            Enumerable.Range(0, concurrentRequests).Select(_ => _client.RegisterAndLoginUserAsync()));

        var notifiedBefore = _factory.Notifier.CallCount;

        // A Barrier, not a plain loop: every participant blocks until all N have arrived,
        // so the write windows genuinely overlap instead of merely being "started fast".
        using var barrier = new Barrier(concurrentRequests);
        var responses = await Task.WhenAll(userTokens.Select(token => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await _client.SendAuthorizedAsync(
                HttpMethod.Post, "/api/v1/bookings", token, new { timeSlotId = slot.Id });
        })));

        // No response is anything other than 201/409 — explicitly rules out a 500 from an
        // unrelated/mistranslated DB error, not just the count assertions below implying it.
        Assert.All(responses, response => Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Unexpected status {(int)response.StatusCode} {response.StatusCode}."));

        var created = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
        var conflicts = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();

        Assert.Single(created);
        Assert.Equal(concurrentRequests - 1, conflicts.Count);

        foreach (var conflict in conflicts)
        {
            var problem = await conflict.Content.ReadFromJsonAsync<ProblemDetailsDto>();
            Assert.Equal("slot_already_booked", problem!.Code);
        }

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bookingCount = await dbContext.Bookings.CountAsync(b => b.TimeSlotId == slot.Id);
        Assert.Equal(1, bookingCount);

        // ADR 0002 step 7 ("no losing request produces a SlotBookingChanged
        // notification"): asserted at the `IBookingNotifier` seam — the same seam
        // `SignalRBookingNotifier` occupies in production — so it holds regardless of the
        // delivery channel. `RealtimeTests` covers the SignalR side over a real client.
        Assert.Equal(1, _factory.Notifier.CallCount - notifiedBefore);
    }

    private async Task<string> AdminTokenAsync() =>
        await _client.LoginAsAdminAsync(_factory.Services.GetRequiredService<IConfiguration>());

    private async Task<(string UserToken, TimeSlotDto Slot)> CreateUserAndFreshSlotAsync()
    {
        var adminToken = await AdminTokenAsync();
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var slot = await _client.CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));
        var userToken = await _client.RegisterAndLoginUserAsync();

        return (userToken, slot);
    }
}

/// <summary>Thread-safe spy so tests can assert `IBookingNotifier` fired exactly once per booking.</summary>
public sealed class CountingBookingNotifier : IBookingNotifier
{
    private int _callCount;

    public int CallCount => _callCount;

    public Task NotifyBookedAsync(
        Guid resourceId, Guid slotId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Same pattern as <see cref="ApiApplicationFactory"/>, plus swapping in a
/// <see cref="CountingBookingNotifier"/> so booking tests can assert notification counts
/// at the seam, without a SignalR client in the loop (`RealtimeTests` covers that end).
/// </summary>
public sealed class BookingApiApplicationFactory(SqlServerContainerFixture sqlFixture) : WebApplicationFactory<Program>
{
    public CountingBookingNotifier Notifier { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", sqlFixture.ConnectionString);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBookingNotifier>();
            services.AddSingleton<IBookingNotifier>(Notifier);
        });
    }
}
