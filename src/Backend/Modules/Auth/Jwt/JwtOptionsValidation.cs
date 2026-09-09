namespace MeetingRoomBooking.Api.Modules.Auth.Jwt;

public static class JwtOptionsValidation
{
    // 32 UTF-8 bytes = 256 bits, the minimum HS256 recommends for its signing key.
    private const int MinimumSigningKeyLength = 32;

    public static JwtOptions Validate(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey) || options.SigningKey.Length < MinimumSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey must be configured and at least {MinimumSigningKeyLength} characters long. " +
                "See README.md for local setup.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");
        }

        return options;
    }
}
