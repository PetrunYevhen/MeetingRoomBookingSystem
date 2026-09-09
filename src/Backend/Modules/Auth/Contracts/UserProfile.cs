namespace MeetingRoomBooking.Api.Modules.Auth.Contracts;

public sealed record UserProfile(Guid Id, string Email, IReadOnlyList<string> Roles);
