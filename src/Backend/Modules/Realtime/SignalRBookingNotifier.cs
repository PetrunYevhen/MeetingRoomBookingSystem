using MeetingRoomBooking.Api.Modules.Bookings;
using MeetingRoomBooking.Api.Modules.Realtime.Contracts;
using MeetingRoomBooking.Api.Modules.Resources.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace MeetingRoomBooking.Api.Modules.Realtime;

/// <summary>
/// The real implementation of `Bookings`' notification seam, replacing
/// `NoOpBookingNotifier` now that a real delivery channel exists — a DI registration
/// change only, `Bookings` itself is untouched.
/// </summary>
public sealed class SignalRBookingNotifier(IHubContext<BookingHub> hubContext) : IBookingNotifier
{
    private const string EventName = "SlotBookingChanged";

    public async Task NotifyBookedAsync(
        Guid resourceId, Guid slotId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        var payload = new SlotBookingChangedEvent(resourceId, slotId, TimeSlotResponse.Booked, occurredAtUtc);

        await hubContext.Clients
            .Group(BookingHub.GroupName(resourceId))
            .SendAsync(EventName, payload, cancellationToken);
    }
}
