namespace MeetingRoomBooking.Api.Configuration;

public static class CorsConfiguration
{
    public const string PolicyName = "Frontend";

    public static IReadOnlyList<string> ValidateAllowedOrigins(IEnumerable<string>? configuredOrigins)
    {
        var origins = configuredOrigins?.ToArray() ?? [];

        if (origins.Length == 0)
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must contain at least one origin.");
        }

        foreach (var origin in origins)
        {
            ValidateOrigin(origin);
        }

        return origins;
    }

    private static void ValidateOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            throw new InvalidOperationException("CORS origins cannot be empty.");
        }

        if (!string.Equals(origin, origin.Trim(), StringComparison.Ordinal) ||
            origin.Contains("*", StringComparison.Ordinal) ||
            origin.Contains("?", StringComparison.Ordinal) ||
            origin.Contains("#", StringComparison.Ordinal) ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" ||
            origin.EndsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"CORS origin '{origin}' is invalid. Configure an exact HTTP or HTTPS origin without a path, query, fragment, credentials, or wildcard.");
        }
    }
}
