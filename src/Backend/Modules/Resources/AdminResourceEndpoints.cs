using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Modules.Auth;
using MeetingRoomBooking.Api.Modules.Resources.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Modules.Resources;

/// <summary>
/// Admin CRUD for rooms and their fixed slots. Deleting a room with slots, or
/// deleting/editing a slot that already has a booking, is rejected — the DB's
/// `DeleteBehavior.Restrict` FKs are the authority; a pre-check just gives a clean
/// response in the common case (same pattern as `AuthEndpoints.RegisterAsync`).
/// </summary>
public static class AdminResourceEndpoints
{
    public static IEndpointRouteBuilder MapAdminResourceEndpoints(this IEndpointRouteBuilder app)
    {
        var resources = app.MapGroup("/api/v1/admin/resources")
            .WithTags("Admin.Resources")
            .RequireAuthorization(Roles.Admin);

        resources.MapPost("/", CreateResourceAsync).WithName("CreateResource").WithOpenApi();
        resources.MapPut("/{resourceId:guid}", UpdateResourceAsync).WithName("UpdateResource").WithOpenApi();
        resources.MapDelete("/{resourceId:guid}", DeleteResourceAsync).WithName("DeleteResource").WithOpenApi();
        resources.MapPost("/{resourceId:guid}/slots", CreateSlotAsync).WithName("CreateSlot").WithOpenApi();

        var slots = app.MapGroup("/api/v1/admin/slots")
            .WithTags("Admin.Resources")
            .RequireAuthorization(Roles.Admin);

        slots.MapPut("/{slotId:guid}", UpdateSlotAsync).WithName("UpdateSlot").WithOpenApi();
        slots.MapDelete("/{slotId:guid}", DeleteSlotAsync).WithName("DeleteSlot").WithOpenApi();

        return app;
    }

    private static async Task<IResult> CreateResourceAsync(
        ResourceRequest request, ApplicationDbContext dbContext, HttpContext httpContext)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return ResourceProblemTypes.ValidationFailed.ToResult(
                StatusCodes.Status400BadRequest, httpContext.Request.Path, "Name is required.");
        }

        var resource = new Resource { Id = Guid.NewGuid(), Name = name };
        dbContext.Resources.Add(resource);
        await dbContext.SaveChangesAsync();

        var response = new ResourceResponse(resource.Id, resource.Name);
        return TypedResults.Created($"/api/v1/resources/{resource.Id}", response);
    }

    private static async Task<IResult> UpdateResourceAsync(
        Guid resourceId, ResourceRequest request, ApplicationDbContext dbContext, HttpContext httpContext)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return ResourceProblemTypes.ValidationFailed.ToResult(
                StatusCodes.Status400BadRequest, httpContext.Request.Path, "Name is required.");
        }

        var resource = await dbContext.Resources.FindAsync(resourceId);
        if (resource is null)
        {
            return TypedResults.NotFound();
        }

        resource.Name = name;
        await dbContext.SaveChangesAsync();

        return TypedResults.Ok(new ResourceResponse(resource.Id, resource.Name));
    }

    private static async Task<IResult> DeleteResourceAsync(
        Guid resourceId, ApplicationDbContext dbContext, HttpContext httpContext)
    {
        var resource = await dbContext.Resources.FindAsync(resourceId);
        if (resource is null)
        {
            return TypedResults.NotFound();
        }

        if (await dbContext.TimeSlots.AnyAsync(slot => slot.ResourceId == resourceId))
        {
            return ResourceProblemTypes.ResourceHasSlots.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        dbContext.Resources.Remove(resource);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            SqlServerExceptions.TryGetViolatedForeignKeyName(exception, out var fk) &&
            fk == "FK_TimeSlots_Resources_ResourceId")
        {
            return ResourceProblemTypes.ResourceHasSlots.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> CreateSlotAsync(
        Guid resourceId, SlotRequest request, ApplicationDbContext dbContext, HttpContext httpContext)
    {
        if (!await dbContext.Resources.AnyAsync(r => r.Id == resourceId))
        {
            return TypedResults.NotFound();
        }

        if (request.EndUtc <= request.StartUtc)
        {
            return ResourceProblemTypes.ValidationFailed.ToResult(
                StatusCodes.Status400BadRequest, httpContext.Request.Path, "endUtc must be after startUtc.");
        }

        var duplicateExists = await dbContext.TimeSlots.AnyAsync(slot =>
            slot.ResourceId == resourceId && slot.StartUtc == request.StartUtc && slot.EndUtc == request.EndUtc);
        if (duplicateExists)
        {
            return ResourceProblemTypes.DuplicateSlot.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        var slot = new TimeSlot
        {
            Id = Guid.NewGuid(),
            ResourceId = resourceId,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
        };
        dbContext.TimeSlots.Add(slot);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            SqlServerExceptions.TryGetViolatedConstraintName(exception, out var index) &&
            index == "UX_TimeSlots_ResourceId_StartUtc_EndUtc")
        {
            return ResourceProblemTypes.DuplicateSlot.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        return TypedResults.Created(
            (string?)null, new TimeSlotResponse(slot.Id, slot.StartUtc, slot.EndUtc, TimeSlotResponse.Available));
    }

    private static async Task<IResult> UpdateSlotAsync(
        Guid slotId, SlotRequest request, ApplicationDbContext dbContext, HttpContext httpContext)
    {
        var slot = await dbContext.TimeSlots.FindAsync(slotId);
        if (slot is null)
        {
            return TypedResults.NotFound();
        }

        if (request.EndUtc <= request.StartUtc)
        {
            return ResourceProblemTypes.ValidationFailed.ToResult(
                StatusCodes.Status400BadRequest, httpContext.Request.Path, "endUtc must be after startUtc.");
        }

        if (await dbContext.Bookings.AnyAsync(booking => booking.TimeSlotId == slotId))
        {
            return ResourceProblemTypes.TimeSlotHasBooking.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        var duplicateExists = await dbContext.TimeSlots.AnyAsync(other =>
            other.Id != slotId && other.ResourceId == slot.ResourceId &&
            other.StartUtc == request.StartUtc && other.EndUtc == request.EndUtc);
        if (duplicateExists)
        {
            return ResourceProblemTypes.DuplicateSlot.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        slot.StartUtc = request.StartUtc;
        slot.EndUtc = request.EndUtc;

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            SqlServerExceptions.TryGetViolatedConstraintName(exception, out var index) &&
            index == "UX_TimeSlots_ResourceId_StartUtc_EndUtc")
        {
            return ResourceProblemTypes.DuplicateSlot.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        return TypedResults.Ok(new TimeSlotResponse(slot.Id, slot.StartUtc, slot.EndUtc, TimeSlotResponse.Available));
    }

    private static async Task<IResult> DeleteSlotAsync(Guid slotId, ApplicationDbContext dbContext, HttpContext httpContext)
    {
        var slot = await dbContext.TimeSlots.FindAsync(slotId);
        if (slot is null)
        {
            return TypedResults.NotFound();
        }

        if (await dbContext.Bookings.AnyAsync(booking => booking.TimeSlotId == slotId))
        {
            return ResourceProblemTypes.TimeSlotHasBooking.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        dbContext.TimeSlots.Remove(slot);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (
            SqlServerExceptions.TryGetViolatedForeignKeyName(exception, out var fk) &&
            fk == "FK_Bookings_TimeSlots_TimeSlotId")
        {
            return ResourceProblemTypes.TimeSlotHasBooking.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        return TypedResults.NoContent();
    }
}
