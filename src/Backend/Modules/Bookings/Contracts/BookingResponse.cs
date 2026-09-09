namespace MeetingRoomBooking.Api.Modules.Bookings.Contracts;

public sealed record BookingResponse(Guid Id, Guid TimeSlotId, DateTime CreatedAtUtc);
