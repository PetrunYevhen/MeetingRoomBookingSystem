using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Bookings;
using MeetingRoomBooking.Api.Modules.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MeetingRoomBooking.Api.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<RefreshTokenSession> RefreshTokenSessions => Set<RefreshTokenSession>();

    /// <summary>
    /// SQL Server's `datetime2` carries no offset, so EF materializes every timestamp with
    /// `DateTimeKind.Unspecified`. Left alone, `System.Text.Json` then writes
    /// `2026-09-09T09:00:00` with no `Z`, and every client — the SPA included — is free to
    /// read a UTC instant as local wall-clock time. Stamping `Utc` on read makes the API's
    /// `...Utc` field names true on the wire; values written are converted to UTC first, so
    /// a local-kind `DateTime` can never be stored as if it were already UTC.
    /// </summary>
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(UtcConverter);
                }
            }
        }
    }
}
