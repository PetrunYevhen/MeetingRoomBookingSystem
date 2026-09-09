using System.Net;
using System.Text.Json;
using MeetingRoomBooking.Api.Modules.Realtime.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>
/// End-to-end proof of ADR 0001's Realtime module against the real hub pipeline (JWT
/// auth, resource groups, JSON protocol) — not just that <c>IBookingNotifier</c> was
/// called (already proven in <c>BookingEndpointsTests</c>), but that a subscribed client
/// actually receives <c>SlotBookingChanged</c> over the wire. `TestServer` doesn't support
/// WebSockets, so these connections use long polling — a well-known, documented
/// workaround for SignalR-over-`WebApplicationFactory` testing, not a departure from how
/// the real transport behaves for a subscribed client.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RealtimeTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public RealtimeTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WatchedResource_ReceivesSlotBookingChanged_WithCorrectPayload()
    {
        var adminToken = await _client.LoginAsAdminAsync(_factory.Services.GetRequiredService<IConfiguration>());
        var (resourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var slot = await _client.CreateSlotAsAdminAsync(adminToken, resourceId, start, start.AddHours(1));

        var userToken = await _client.RegisterAndLoginUserAsync();
        await using var connection = CreateConnection(userToken);

        var received = new TaskCompletionSource<SlotBookingChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<SlotBookingChangedEvent>("SlotBookingChanged", e => received.TrySetResult(e));

        await connection.StartAsync();
        await connection.InvokeAsync("WatchResource", resourceId);

        var bookingResponse = await _client.SendAuthorizedAsync(
            HttpMethod.Post, "/api/v1/bookings", userToken, new { timeSlotId = slot.Id });
        Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);

        var slotEvent = await WaitAsync(received.Task);

        Assert.Equal(resourceId, slotEvent.ResourceId);
        Assert.Equal(slot.Id, slotEvent.SlotId);
        Assert.Equal("booked", slotEvent.Status);
    }

    [Fact]
    public async Task WatchingDifferentResource_DoesNotReceiveEvent()
    {
        var adminToken = await _client.LoginAsAdminAsync(_factory.Services.GetRequiredService<IConfiguration>());
        var (bookedResourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var (otherResourceId, _) = await _client.CreateResourceAsAdminAsync(adminToken);
        var start = DateTime.UtcNow.AddDays(1);
        var slot = await _client.CreateSlotAsAdminAsync(adminToken, bookedResourceId, start, start.AddHours(1));

        var userToken = await _client.RegisterAndLoginUserAsync();
        await using var connection = CreateConnection(userToken);

        var received = false;
        connection.On<SlotBookingChangedEvent>("SlotBookingChanged", _ => received = true);

        await connection.StartAsync();
        await connection.InvokeAsync("WatchResource", otherResourceId);

        var bookingResponse = await _client.SendAuthorizedAsync(
            HttpMethod.Post, "/api/v1/bookings", userToken, new { timeSlotId = slot.Id });
        Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);

        // No positive "nothing happened" signal exists beyond a bounded wait: give the
        // delivery pipeline a fair chance, then assert it never arrived at this connection.
        await Task.Delay(500);
        Assert.False(received);
    }

    [Fact]
    public async Task Connect_WithoutToken_IsRejected()
    {
        await using var connection = CreateConnection(accessToken: null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task WatchResource_UnknownResource_Throws()
    {
        var userToken = await _client.RegisterAndLoginUserAsync();
        await using var connection = CreateConnection(userToken);
        await connection.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("WatchResource", Guid.NewGuid()));
    }

    private HubConnection CreateConnection(string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "/hubs/bookings"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (accessToken is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                }
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            .Build();

    private static async Task<T> WaitAsync<T>(Task<T> task, int timeoutMs = 5000)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
        Assert.True(completed == task, "Timed out waiting for the SignalR event.");
        return await task;
    }
}
