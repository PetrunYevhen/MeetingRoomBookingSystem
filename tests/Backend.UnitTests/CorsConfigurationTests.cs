using MeetingRoomBooking.Api.Configuration;
using Xunit;

namespace Backend.UnitTests;

public sealed class CorsConfigurationTests
{
    [Fact]
    public void ValidateAllowedOrigins_ReturnsExactValidOrigins()
    {
        string[] configuredOrigins = ["http://localhost:5173", "https://booking.example.com"];

        var origins = CorsConfiguration.ValidateAllowedOrigins(configuredOrigins);

        Assert.Equal(configuredOrigins, origins);
    }

    [Fact]
    public void ValidateAllowedOrigins_RejectsMissingOrigins()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CorsConfiguration.ValidateAllowedOrigins(null));

        Assert.Contains("at least one origin", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/")]
    [InlineData("not-an-origin")]
    public void ValidateAllowedOrigins_RejectsInvalidOrigin(string invalidOrigin)
    {
        Assert.Throws<InvalidOperationException>(
            () => CorsConfiguration.ValidateAllowedOrigins([invalidOrigin]));
    }
}
