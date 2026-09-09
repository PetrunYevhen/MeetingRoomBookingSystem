using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetingRoomBooking.Api.Modules.Resources;

public sealed class TimeSlotEntityConfiguration : IEntityTypeConfiguration<TimeSlot>
{
    public void Configure(EntityTypeBuilder<TimeSlot> builder)
    {
        builder.ToTable("TimeSlots", table =>
            table.HasCheckConstraint("CK_TimeSlots_EndAfterStart", "[EndUtc] > [StartUtc]"));

        builder.HasKey(timeSlot => timeSlot.Id);

        builder.Property(timeSlot => timeSlot.StartUtc).IsRequired();

        builder.Property(timeSlot => timeSlot.EndUtc).IsRequired();

        builder.Property(timeSlot => timeSlot.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .ValueGeneratedOnAdd();

        // Supports schedule queries filtered by a UTC date range (ADR 0001).
        builder.HasIndex(timeSlot => new { timeSlot.ResourceId, timeSlot.StartUtc })
            .HasDatabaseName("IX_TimeSlots_ResourceId_StartUtc");

        // Prevents an admin from creating the exact same slot twice.
        builder.HasIndex(timeSlot => new { timeSlot.ResourceId, timeSlot.StartUtc, timeSlot.EndUtc })
            .IsUnique()
            .HasDatabaseName("UX_TimeSlots_ResourceId_StartUtc_EndUtc");
    }
}
