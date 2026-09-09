namespace MeetingRoomBooking.Api.Modules.Auth.Contracts;

public sealed record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, UserProfile User);
