using MeetingRoomBooking.Api.Modules.Resources;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Infrastructure.Persistence.Seed;

/// <summary>
/// Demo rooms and a rolling window of bookable slots, so a freshly migrated database —
/// local or deployed — always has something to view and book. Runs in every environment
/// (see Program.cs): the deployed app is the reviewer's entry point and an admin-only
/// REST call must not be the precondition for it showing anything at all.
///
/// Idempotent in two ways: rooms are created by name only when missing, and each day's
/// slots are inserted only when that exact window doesn't exist yet. Because the window
/// is relative to "today", every startup tops it back up instead of leaving a database
/// seeded once and stale three days later.
/// </summary>
public static class SampleDataSeeder
{
    private static readonly string[] SampleResourceNames = ["Falcon", "Phoenix"];

    /// <summary>Slot start hours (UTC) offered on each seeded day.</summary>
    private static readonly int[] SlotStartHours = [9, 11, 14, 16];

    private const int SeededDays = 3;

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        foreach (var name in SampleResourceNames)
        {
            var resource = await context.Resources
                .SingleOrDefaultAsync(candidate => candidate.Name == name, cancellationToken);

            if (resource is null)
            {
                resource = new Resource { Id = Guid.NewGuid(), Name = name };
                context.Resources.Add(resource);
            }

            var existingStarts = await context.TimeSlots
                .Where(slot => slot.ResourceId == resource.Id && slot.StartUtc >= today)
                .Select(slot => slot.StartUtc)
                .ToListAsync(cancellationToken);

            for (var day = 1; day <= SeededDays; day++)
            {
                var date = today.AddDays(day);

                foreach (var hour in SlotStartHours)
                {
                    var startUtc = date.AddHours(hour);
                    if (existingStarts.Contains(startUtc))
                    {
                        continue;
                    }

                    context.TimeSlots.Add(new TimeSlot
                    {
                        Id = Guid.NewGuid(),
                        ResourceId = resource.Id,
                        StartUtc = startUtc,
                        EndUtc = startUtc.AddHours(1),
                    });
                }
            }
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            SqlServerExceptions.TryGetViolatedConstraintName(exception, out var index) &&
            index == "UX_TimeSlots_ResourceId_StartUtc_EndUtc")
        {
            // Two instances started at once and both topped up the same day. The unique
            // index kept the schedule correct; the loser has nothing left to do.
            context.ChangeTracker.Clear();
        }
    }
}
