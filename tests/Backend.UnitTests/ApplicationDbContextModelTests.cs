using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Bookings;
using MeetingRoomBooking.Api.Modules.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Backend.UnitTests;

/// <summary>
/// Asserts the schema contract from ADR 0002 directly on the EF Core model, without a
/// database: the unique index is the single authority for "one slot, one booking", and
/// the FKs/check constraint that back the rest of the integrity guarantees are present.
/// Building the model does not open a connection, so this stays a fast unit test.
/// </summary>
public sealed class ApplicationDbContextModelTests
{
    // Check constraints only live in the design-time model, not the runtime-optimized
    // one exposed by DbContext.Model.
    private static readonly IModel Model = CreateContext().GetService<IDesignTimeModel>().Model;

    [Fact]
    public void Bookings_HasUniqueIndexOnTimeSlotId()
    {
        var bookingType = Model.FindEntityType(typeof(Booking))!;

        var index = bookingType.GetIndexes().Single(i => i.GetDatabaseName() == "UX_Bookings_TimeSlotId");

        Assert.True(index.IsUnique);
        Assert.Equal(nameof(Booking.TimeSlotId), Assert.Single(index.Properties).Name);
    }

    [Fact]
    public void TimeSlots_HasUniqueIndexPreventingDuplicateSlots()
    {
        var timeSlotType = Model.FindEntityType(typeof(TimeSlot))!;

        var index = timeSlotType.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_TimeSlots_ResourceId_StartUtc_EndUtc");

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void TimeSlots_HasCheckConstraint_EndAfterStart()
    {
        var timeSlotType = Model.FindEntityType(typeof(TimeSlot))!;

        var constraint = timeSlotType.GetCheckConstraints().Single(c => c.Name == "CK_TimeSlots_EndAfterStart");

        Assert.Contains("EndUtc", constraint.Sql);
        Assert.Contains("StartUtc", constraint.Sql);
    }

    [Fact]
    public void RefreshTokenSessions_HasUniqueIndexOnTokenHash()
    {
        var sessionType = Model.FindEntityType(typeof(RefreshTokenSession))!;

        var index = sessionType.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_RefreshTokenSessions_TokenHash");

        Assert.True(index.IsUnique);
    }

    [Theory]
    [InlineData(typeof(TimeSlot), nameof(TimeSlot.ResourceId))]
    [InlineData(typeof(Booking), nameof(Booking.TimeSlotId))]
    [InlineData(typeof(Booking), nameof(Booking.UserId))]
    [InlineData(typeof(RefreshTokenSession), nameof(RefreshTokenSession.UserId))]
    public void ForeignKeys_RestrictDelete(Type dependentClrType, string foreignKeyProperty)
    {
        var dependentType = Model.FindEntityType(dependentClrType)!;

        var foreignKey = dependentType.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == foreignKeyProperty));

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=.;Database=ModelBuildOnly;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new ApplicationDbContext(options);
    }
}
