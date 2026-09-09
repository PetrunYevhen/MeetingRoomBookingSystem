using MeetingRoomBooking.Api.Infrastructure.Http;

namespace MeetingRoomBooking.Api.Modules.Bookings;

/// <summary>
/// The RFC 7807 problem type from ADR 0002's exact example — the losing side of a
/// concurrent booking race, never to be confused with the Resources module's own
/// admin-operation conflicts.
/// </summary>
public static class BookingProblemTypes
{
    public static readonly ProblemType SlotAlreadyBooked = new(
        "/problems/slot-already-booked", "The time slot is already booked.", "slot_already_booked");
}
