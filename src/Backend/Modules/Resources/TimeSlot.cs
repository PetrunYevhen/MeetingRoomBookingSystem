using MeetingRoomBooking.Api.Modules.Bookings;

namespace MeetingRoomBooking.Api.Modules.Resources;

public sealed class TimeSlot
{
    public Guid Id { get; set; }

    public Guid ResourceId { get; set; }

    public Resource? Resource { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Booking? Booking { get; set; }
}
