using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Resources.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Modules.Resources;

/// <summary>
/// Public read surface: any authenticated user (or admin) can browse rooms and their
/// schedules. Booking a slot is the `Bookings` module's job, out of scope here.
/// </summary>
public static class ResourceEndpoints
{
    public static IEndpointRouteBuilder MapResourceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/resources")
            .WithTags("Resources")
            .RequireAuthorization(Roles.User);

        group.MapGet("/", ListResourcesAsync).WithName("ListResources").WithOpenApi();
        group.MapGet("/{resourceId:guid}", GetResourceAsync).WithName("GetResource").WithOpenApi();
        group.MapGet("/{resourceId:guid}/slots", GetResourceSlotsAsync).WithName("GetResourceSlots").WithOpenApi();

        return app;
    }

    private static async Task<IResult> ListResourcesAsync(ApplicationDbContext dbContext)
    {
        var resources = await dbContext.Resources
            .OrderBy(resource => resource.Name)
            .Select(resource => new ResourceResponse(resource.Id, resource.Name))
            .ToListAsync();

        return TypedResults.Ok(resources);
    }

    private static async Task<IResult> GetResourceAsync(Guid resourceId, ApplicationDbContext dbContext)
    {
        var resource = await dbContext.Resources
            .Where(r => r.Id == resourceId)
            .Select(r => new ResourceResponse(r.Id, r.Name))
            .SingleOrDefaultAsync();

        return resource is null ? TypedResults.NotFound() : TypedResults.Ok(resource);
    }

    private static async Task<IResult> GetResourceSlotsAsync(
        Guid resourceId, DateTime? fromUtc, DateTime? toUtc, ApplicationDbContext dbContext)
    {
        if (!await dbContext.Resources.AnyAsync(r => r.Id == resourceId))
        {
            return TypedResults.NotFound();
        }

        var slots = await dbContext.TimeSlots
            .Where(slot => slot.ResourceId == resourceId)
            .Where(slot => fromUtc == null || slot.StartUtc >= fromUtc)
            .Where(slot => toUtc == null || slot.StartUtc < toUtc)
            .OrderBy(slot => slot.StartUtc)
            .Select(slot => new TimeSlotResponse(
                slot.Id,
                slot.StartUtc,
                slot.EndUtc,
                slot.Booking != null ? TimeSlotResponse.Booked : TimeSlotResponse.Available))
            .ToListAsync();

        return TypedResults.Ok(slots);
    }
}
