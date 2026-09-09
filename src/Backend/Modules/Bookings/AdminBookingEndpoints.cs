using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Bookings.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Modules.Bookings;

/// <summary>
/// The admin's cross-user read surface: "view all bookings across users" from the task
/// statement. Regular users never reach it — every route is behind the `Admin` policy,
/// and `POST /api/v1/bookings` (the `User` surface) stays untouched.
/// </summary>
public static class AdminBookingEndpoints
{
    public static IEndpointRouteBuilder MapAdminBookingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/bookings", ListBookingsAsync)
            .WithTags("Admin.Bookings")
            .RequireAuthorization(Roles.Admin)
            .WithName("ListAllBookings")
            .WithOpenApi();

        return app;
    }

    /// <summary>Default page size; also the cap, so an admin can never pull an unbounded result set.</summary>
    private const int DefaultPageSize = 100;

    private static async Task<IResult> ListBookingsAsync(
        Guid? resourceId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? skip,
        int? take,
        ApplicationDbContext dbContext)
    {
        // ADR 0001 specifies filtering *and* pagination here. Out-of-range values are
        // clamped rather than rejected: a paging hint is not worth a 400, and clamping
        // is what keeps the response bounded no matter what the client asks for.
        var pageOffset = Math.Max(skip ?? 0, 0);
        var pageSize = Math.Clamp(take ?? DefaultPageSize, 1, DefaultPageSize);

        var bookings = await dbContext.Bookings
            .Where(booking => resourceId == null || booking.TimeSlot!.ResourceId == resourceId)
            .Where(booking => fromUtc == null || booking.TimeSlot!.StartUtc >= fromUtc)
            .Where(booking => toUtc == null || booking.TimeSlot!.StartUtc < toUtc)
            .OrderBy(booking => booking.TimeSlot!.StartUtc)
            .ThenBy(booking => booking.Id)
            .Skip(pageOffset)
            .Take(pageSize)
            .Select(booking => new AdminBookingResponse(
                booking.Id,
                booking.TimeSlot!.ResourceId,
                booking.TimeSlot!.Resource!.Name,
                booking.TimeSlotId,
                booking.TimeSlot!.StartUtc,
                booking.TimeSlot!.EndUtc,
                booking.UserId,
                booking.User!.Email!,
                booking.CreatedAtUtc))
            .ToListAsync();

        return TypedResults.Ok(bookings);
    }
}
