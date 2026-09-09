namespace MeetingRoomBooking.Api.Modules.Auth;

public static class Roles
{
    public const string User = "User";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [User, Admin];
}
