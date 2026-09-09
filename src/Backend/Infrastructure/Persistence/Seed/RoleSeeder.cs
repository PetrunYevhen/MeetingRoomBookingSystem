using MeetingRoomBooking.Api.Modules.Auth;
using Microsoft.AspNetCore.Identity;

namespace MeetingRoomBooking.Api.Infrastructure.Persistence.Seed;

public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }
}
