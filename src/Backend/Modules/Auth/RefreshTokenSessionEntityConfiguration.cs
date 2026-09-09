using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetingRoomBooking.Api.Modules.Auth;

public sealed class RefreshTokenSessionEntityConfiguration : IEntityTypeConfiguration<RefreshTokenSession>
{
    public void Configure(EntityTypeBuilder<RefreshTokenSession> builder)
    {
        builder.ToTable("RefreshTokenSessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.TokenHash)
            .IsRequired()
            .HasMaxLength(44); // Base64-encoded SHA-256 digest.

        builder.Property(session => session.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .ValueGeneratedOnAdd();

        builder.Property(session => session.ExpiresAtUtc).IsRequired();

        builder.HasIndex(session => session.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_RefreshTokenSessions_TokenHash");

        builder.HasIndex(session => session.FamilyId)
            .HasDatabaseName("IX_RefreshTokenSessions_FamilyId");

        builder.HasIndex(session => session.UserId)
            .HasDatabaseName("IX_RefreshTokenSessions_UserId");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
