using MeetingRoomBooking.Api.Modules.Resources;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only convenience data so a freshly migrated database has something to
/// view and book. Never runs outside Development (see Program.cs).
/// </summary>
public static class DevelopmentDataSeeder
{
    private static readonly string[] SampleResourceNames = ["Falcon", "Phoenix"];

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Resources.AnyAsync(cancellationToken))
        {
            return;
        }

        var today = DateTime.UtcNow.Date;

        foreach (var name in SampleResourceNames)
        {
            var resource = new Resource { Id = Guid.NewGuid(), Name = name };

            for (var day = 1; day <= 3; day++)
            {
                var date = today.AddDays(day);
                resource.TimeSlots.Add(CreateSlot(resource.Id, date.AddHours(9), date.AddHours(10)));
                resource.TimeSlots.Add(CreateSlot(resource.Id, date.AddHours(14), date.AddHours(15)));
            }

            context.Resources.Add(resource);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static TimeSlot CreateSlot(Guid resourceId, DateTime startUtc, DateTime endUtc) => new()
    {
        Id = Guid.NewGuid(),
        ResourceId = resourceId,
        StartUtc = startUtc,
        EndUtc = endUtc,
    };
}
