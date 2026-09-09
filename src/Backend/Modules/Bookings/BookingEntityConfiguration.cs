using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetingRoomBooking.Api.Modules.Bookings;

public sealed class BookingEntityConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");

        builder.HasKey(booking => booking.Id);

        builder.Property(booking => booking.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .ValueGeneratedOnAdd();

        // The single authority for "one slot, one booking" (ADR 0002). No pre-check
        // substitutes for this: concurrent inserts race on this index, and the loser
        // is translated to a 409 slot-already-booked conflict.
        builder.HasIndex(booking => booking.TimeSlotId)
            .IsUnique()
            .HasDatabaseName("UX_Bookings_TimeSlotId");

        builder.HasIndex(booking => booking.UserId)
            .HasDatabaseName("IX_Bookings_UserId");

        builder.HasOne(booking => booking.TimeSlot)
            .WithOne(timeSlot => timeSlot.Booking)
            .HasForeignKey<Booking>(booking => booking.TimeSlotId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(booking => booking.User)
            .WithMany()
            .HasForeignKey(booking => booking.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
