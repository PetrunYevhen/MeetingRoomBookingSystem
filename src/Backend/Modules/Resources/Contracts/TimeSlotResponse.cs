namespace MeetingRoomBooking.Api.Modules.Resources.Contracts;

public sealed record TimeSlotResponse(Guid Id, DateTime StartUtc, DateTime EndUtc, string Status)
{
    public const string Available = "available";
    public const string Booked = "booked";
}
