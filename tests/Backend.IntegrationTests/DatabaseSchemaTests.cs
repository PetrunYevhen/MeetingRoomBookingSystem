using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Bookings;
using MeetingRoomBooking.Api.Modules.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>
/// Proves the ADR 0002 database invariant against a real SQL Server (never
/// InMemory/SQLite): the unique index, not application code, is what makes a second
/// booking for an already-booked slot impossible. This exercises the same insert path
/// the booking endpoint will use once it exists, ahead of that endpoint landing.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class DatabaseSchemaTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public DatabaseSchemaTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Migrations_ApplyCleanly_AgainstRealSqlServer()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.True(await dbContext.Database.CanConnectAsync());

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains(appliedMigrations, id => id.EndsWith("InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Booking_SecondInsertForSameTimeSlot_ThrowsOnUniqueIndex()
    {
        var (timeSlotId, userId) = await SeedResourceSlotAndUserAsync();

        using (var firstScope = _factory.Services.CreateScope())
        {
            var dbContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Bookings.Add(new Booking { Id = Guid.NewGuid(), TimeSlotId = timeSlotId, UserId = userId });
            await dbContext.SaveChangesAsync();
        }

        using (var secondScope = _factory.Services.CreateScope())
        {
            var dbContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Bookings.Add(new Booking { Id = Guid.NewGuid(), TimeSlotId = timeSlotId, UserId = userId });

            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        }

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var dbContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var bookingCount = await dbContext.Bookings.CountAsync(booking => booking.TimeSlotId == timeSlotId);
            Assert.Equal(1, bookingCount);
        }
    }

    private async Task<(Guid TimeSlotId, Guid UserId)> SeedResourceSlotAndUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var resource = new Resource { Id = Guid.NewGuid(), Name = $"Schema Test Room {Guid.NewGuid()}" };
        var timeSlot = new TimeSlot
        {
            Id = Guid.NewGuid(),
            ResourceId = resource.Id,
            StartUtc = DateTime.UtcNow.AddDays(1),
            EndUtc = DateTime.UtcNow.AddDays(1).AddHours(1),
        };
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"schema-test-{Guid.NewGuid()}@example.com",
            Email = $"schema-test-{Guid.NewGuid()}@example.com",
        };

        dbContext.Resources.Add(resource);
        dbContext.TimeSlots.Add(timeSlot);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return (timeSlot.Id, user.Id);
    }
}
