namespace MeetingRoomBooking.Api.Modules.Bookings;

/// <summary>
/// Placeholder <see cref="IBookingNotifier"/> until the `Realtime` module exists. Lets
/// `Bookings` ship and be fully tested on its own, per ADR 0001's module boundary.
/// </summary>
public sealed class NoOpBookingNotifier : IBookingNotifier
{
    public Task NotifyBookedAsync(Guid resourceId, Guid slotId, DateTime occurredAtUtc, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
