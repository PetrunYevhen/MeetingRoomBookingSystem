using Microsoft.AspNetCore.Http.HttpResults;

namespace MeetingRoomBooking.Api.Infrastructure.Http;

/// <summary>
/// A named RFC 7807 problem shape, reused across modules so each module's conflicts stay
/// distinguishable by `type`/`code` instead of collapsing into generic 400/409 responses.
/// </summary>
public sealed record ProblemType(string Type, string Title, string Code)
{
    public ProblemHttpResult ToResult(int statusCode, string instance, string? detail = null) =>
        TypedResults.Problem(
            type: Type,
            title: Title,
            statusCode: statusCode,
            detail: detail,
            instance: instance,
            extensions: new Dictionary<string, object?> { ["code"] = Code });
}
