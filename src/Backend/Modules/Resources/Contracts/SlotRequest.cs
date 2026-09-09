namespace MeetingRoomBooking.Api.Modules.Resources.Contracts;

public sealed record SlotRequest(DateTime StartUtc, DateTime EndUtc);
