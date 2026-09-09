using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Bookings;
using MeetingRoomBooking.Api.Modules.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<RefreshTokenSession> RefreshTokenSessions => Set<RefreshTokenSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
