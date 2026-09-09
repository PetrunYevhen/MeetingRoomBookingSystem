using MeetingRoomBooking.Api.Infrastructure.Http;

namespace MeetingRoomBooking.Api.Modules.Resources;

/// <summary>
/// RFC 7807 problem types for the Resources module's own domain conflicts — distinct from
/// Bookings' future `/problems/slot-already-booked` (ADR 0002), never to be confused with
/// it: these are about admin operations blocked by existing children, not a booking race.
/// </summary>
public static class ResourceProblemTypes
{
    public static readonly ProblemType ValidationFailed = new(
        "/problems/validation-failed", "The request is invalid.", "validation_failed");

    public static readonly ProblemType ResourceHasSlots = new(
        "/problems/resource-has-slots", "The room still has time slots and cannot be removed.", "resource_has_slots");

    public static readonly ProblemType DuplicateSlot = new(
        "/problems/duplicate-slot", "The room already has a slot for that exact time range.", "duplicate_slot");

    public static readonly ProblemType TimeSlotHasBooking = new(
        "/problems/timeslot-has-booking", "The slot already has a booking and cannot be changed.", "timeslot_has_booking");
}
