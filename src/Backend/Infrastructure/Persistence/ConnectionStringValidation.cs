namespace MeetingRoomBooking.Api.Infrastructure.Persistence;

public static class ConnectionStringValidation
{
    public static string ValidateConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default must be configured. See README.md for local setup via docker-compose.");
        }

        return connectionString;
    }
}
