using MeetingRoomBooking.Api.Infrastructure.Http;

namespace MeetingRoomBooking.Api.Modules.Auth;

/// <summary>
/// RFC 7807 problem types for the Auth module's own conflicts — distinct from Bookings'
/// future `/problems/slot-already-booked` (ADR 0002), never to be confused with it.
/// </summary>
public static class AuthProblemTypes
{
    public static readonly ProblemType RegistrationFailed = new(
        "/problems/registration-failed", "Registration failed.", "registration_failed");

    public static readonly ProblemType EmailAlreadyRegistered = new(
        "/problems/email-already-registered", "The email address is already registered.", "email_already_registered");

    public static readonly ProblemType InvalidCredentials = new(
        "/problems/invalid-credentials", "The email or password is incorrect.", "invalid_credentials");

    public static readonly ProblemType InvalidRefreshToken = new(
        "/problems/invalid-refresh-token", "The refresh token is missing, expired, or invalid.", "invalid_refresh_token");

    public static readonly ProblemType OriginNotAllowed = new(
        "/problems/origin-not-allowed", "The request Origin is missing or not allowed.", "origin_not_allowed");
}
