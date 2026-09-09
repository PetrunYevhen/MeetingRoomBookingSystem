using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Bookings.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Modules.Bookings;

/// <summary>
/// The concurrency-critical endpoint. Follows ADR 0002's pseudocode exactly: a read-only
/// existence check (never the concurrency guard) → explicit transaction → insert → commit
/// → notify after commit. The unique index `UX_Bookings_TimeSlotId` is the sole authority
/// for "one slot, one booking"; this code only translates its violation into a 409.
/// </summary>
public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/bookings", CreateBookingAsync)
            .WithTags("Bookings")
            .RequireAuthorization(Roles.User)
            .WithName("CreateBooking")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> CreateBookingAsync(
        CreateBookingRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext dbContext,
        IBookingNotifier notifier,
        ILogger<Booking> logger,
        HttpContext httpContext)
    {
        var userId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        var slot = await dbContext.TimeSlots
            .Where(s => s.Id == request.TimeSlotId)
            .Select(s => new { s.Id, s.ResourceId })
            .SingleOrDefaultAsync();

        if (slot is null)
        {
            return TypedResults.NotFound();
        }

        var booking = new Booking { Id = Guid.NewGuid(), TimeSlotId = slot.Id, UserId = userId };

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            SqlServerExceptions.TryGetViolatedConstraintName(exception, out var index) &&
            index == "UX_Bookings_TimeSlotId")
        {
            // Rolled back by the `await using` disposal below. The unique index — not this
            // catch — is what actually prevented the double booking; this only translates
            // its violation into the documented conflict response.
            return BookingProblemTypes.SlotAlreadyBooked.ToResult(
                StatusCodes.Status409Conflict,
                httpContext.Request.Path,
                "Another booking was committed for the requested time slot.");
        }

        await transaction.CommitAsync();

        try
        {
            await notifier.NotifyBookedAsync(slot.ResourceId, slot.Id, booking.CreatedAtUtc);
        }
        catch (Exception exception)
        {
            // The booking is already committed and valid; a notification failure must not
            // roll back or misreport it (ADR 0002).
            logger.LogWarning(exception, "Failed to publish SlotBookingChanged for slot {SlotId}.", slot.Id);
        }

        return TypedResults.Created(
            (string?)null, new BookingResponse(booking.Id, booking.TimeSlotId, booking.CreatedAtUtc));
    }
}
