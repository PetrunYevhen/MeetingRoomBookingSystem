using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Resources;

namespace MeetingRoomBooking.Api.Modules.Bookings;

public sealed class Booking
{
    public Guid Id { get; set; }

    public Guid TimeSlotId { get; set; }

    public TimeSlot? TimeSlot { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
