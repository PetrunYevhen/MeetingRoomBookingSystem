namespace MeetingRoomBooking.Api.Modules.Auth;

/// <summary>
/// ADR 0001: cookie-authenticated state-changing requests (refresh, logout) must carry
/// the production `Origin` header and it must match a configured frontend origin exactly.
/// CORS alone doesn't enforce this — it only affects what a browser exposes to script, not
/// whether the server accepts the request — so this is an explicit server-side check.
/// </summary>
public sealed class RequireAllowedOriginFilter(IReadOnlyList<string> allowedOrigins) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var origin = context.HttpContext.Request.Headers.Origin.ToString();

        if (string.IsNullOrEmpty(origin) || !allowedOrigins.Contains(origin, StringComparer.Ordinal))
        {
            return ValueTask.FromResult<object?>(AuthProblemTypes.OriginNotAllowed.ToResult(
                StatusCodes.Status403Forbidden, context.HttpContext.Request.Path));
        }

        return next(context);
    }
}
