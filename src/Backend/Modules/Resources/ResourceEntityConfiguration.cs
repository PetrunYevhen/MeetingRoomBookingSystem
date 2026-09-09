using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetingRoomBooking.Api.Modules.Resources;

public sealed class ResourceEntityConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources");

        builder.HasKey(resource => resource.Id);

        builder.Property(resource => resource.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(resource => resource.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .ValueGeneratedOnAdd();

        builder.HasMany(resource => resource.TimeSlots)
            .WithOne(timeSlot => timeSlot.Resource)
            .HasForeignKey(timeSlot => timeSlot.ResourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
