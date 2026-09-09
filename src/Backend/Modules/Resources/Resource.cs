namespace MeetingRoomBooking.Api.Modules.Resources;

public sealed class Resource
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<TimeSlot> TimeSlots { get; set; } = [];
}
