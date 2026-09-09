namespace MeetingRoomBooking.Api.Modules.Bookings.Contracts;

/// <summary>
/// The admin-only cross-user view of a booking: unlike <see cref="BookingResponse"/> it
/// carries who booked what, denormalized (room name, slot window, user email) so the
/// admin screen renders the list without N+1 lookups.
/// </summary>
public sealed record AdminBookingResponse(
    Guid Id,
    Guid ResourceId,
    string ResourceName,
    Guid TimeSlotId,
    DateTime StartUtc,
    DateTime EndUtc,
    Guid UserId,
    string UserEmail,
    DateTime CreatedAtUtc);
