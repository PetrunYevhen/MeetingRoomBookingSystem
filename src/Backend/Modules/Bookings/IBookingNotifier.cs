namespace MeetingRoomBooking.Api.Modules.Bookings;

/// <summary>
/// `Bookings`' output seam for `Realtime` (ADR 0001: dependencies flow into `Realtime`,
/// never out of it — `Bookings` must not reference SignalR). Called only after a booking's
/// transaction commits; a delivery failure must never affect the booking response.
/// </summary>
public interface IBookingNotifier
{
    Task NotifyBookedAsync(Guid resourceId, Guid slotId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);
}
