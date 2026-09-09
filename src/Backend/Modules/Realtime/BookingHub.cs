using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Modules.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Modules.Realtime;

/// <summary>
/// Resource viewer groups (ADR 0001): a client asks to watch a resource, the server
/// validates it and owns the group name — clients never choose an arbitrary delivery
/// group. Disconnecting removes the connection from its groups automatically; no manual
/// cleanup needed for that case.
/// </summary>
[Authorize(Policy = Roles.User)]
public sealed class BookingHub(ApplicationDbContext dbContext) : Hub
{
    public async Task WatchResource(Guid resourceId)
    {
        if (!await dbContext.Resources.AnyAsync(resource => resource.Id == resourceId))
        {
            throw new HubException("Resource not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(resourceId));
    }

    public async Task UnwatchResource(Guid resourceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(resourceId));
    }

    public static string GroupName(Guid resourceId) => $"resource:{resourceId}";
}
