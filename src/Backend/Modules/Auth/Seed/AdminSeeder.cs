using Microsoft.AspNetCore.Identity;

namespace MeetingRoomBooking.Api.Modules.Auth.Seed;

/// <summary>
/// Provisions the initial administrator from configuration rather than client input
/// (ADR 0001: "administrators are provisioned through a controlled deployment/seed
/// process"). Idempotent — does nothing once an account with the configured email
/// exists. Missing configuration is a warning, not a startup failure: unlike the DB
/// connection string, no admin account yet is a recoverable state, not a broken app.
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedAsync(UserManager<ApplicationUser> userManager, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Admin:Email/Admin:Password are not configured; skipping admin bootstrap.");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Admin bootstrap failed: {Errors}", string.Join(" ", result.Errors.Select(error => error.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
    }
}
