using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MeetingRoomBooking.Api.Infrastructure.Persistence;
using MeetingRoomBooking.Api.Modules.Auth.Contracts;
using MeetingRoomBooking.Api.Modules.Auth.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Modules.Auth;

public static class AuthEndpoints
{
    private const string RefreshTokenCookieName = "refreshToken";
    private const string RefreshTokenCookiePath = "/api/v1/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder app, IReadOnlyList<string> allowedOrigins)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync).WithName("Register").WithOpenApi();
        group.MapPost("/login", LoginAsync).WithName("Login").WithOpenApi();

        group.MapPost("/refresh", RefreshAsync)
            .AddEndpointFilter(new RequireAllowedOriginFilter(allowedOrigins))
            .WithName("Refresh")
            .WithOpenApi();

        group.MapPost("/logout", LogoutAsync)
            .AddEndpointFilter(new RequireAllowedOriginFilter(allowedOrigins))
            .WithName("Logout")
            .WithOpenApi();

        group.MapGet("/me", MeAsync)
            .RequireAuthorization(Roles.User)
            .WithName("Me")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, UserManager<ApplicationUser> userManager, HttpContext httpContext)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return AuthProblemTypes.EmailAlreadyRegistered.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = request.Email, Email = request.Email };

        IdentityResult createResult;
        try
        {
            createResult = await userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException exception) when (
            SqlServerExceptions.TryGetViolatedConstraintName(exception, out var constraintName) &&
            constraintName == "UserNameIndex")
        {
            // The FindByEmailAsync pre-check above is only a fast UX path; two concurrent
            // registrations for the same email race here, and the unique index — not the
            // pre-check — is what actually prevents the duplicate (same principle as the
            // Booking unique index in ADR 0002).
            return AuthProblemTypes.EmailAlreadyRegistered.ToResult(
                StatusCodes.Status409Conflict, httpContext.Request.Path);
        }

        if (!createResult.Succeeded)
        {
            return AuthProblemTypes.RegistrationFailed.ToResult(
                StatusCodes.Status400BadRequest,
                httpContext.Request.Path,
                detail: string.Join(" ", createResult.Errors.Select(error => error.Description)));
        }

        await userManager.AddToRoleAsync(user, Roles.User);

        return TypedResults.Created((string?)null, new UserProfile(user.Id, user.Email!, [Roles.User]));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        RefreshTokenService refreshTokenService,
        JwtOptions jwtOptions,
        HttpContext httpContext)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return AuthProblemTypes.InvalidCredentials.ToResult(
                StatusCodes.Status401Unauthorized, httpContext.Request.Path);
        }

        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresAtUtc) = jwtTokenService.CreateAccessToken(user.Id, roles);
        var refreshToken = await refreshTokenService.IssueNewSessionAsync(user.Id);

        SetRefreshTokenCookie(httpContext, refreshToken, jwtOptions.RefreshTokenLifetimeDays);

        return TypedResults.Ok(new AuthResponse(accessToken, expiresAtUtc, new UserProfile(user.Id, user.Email!, [.. roles])));
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext httpContext,
        RefreshTokenService refreshTokenService,
        JwtTokenService jwtTokenService,
        JwtOptions jwtOptions,
        UserManager<ApplicationUser> userManager)
    {
        if (!httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var rawToken) ||
            string.IsNullOrEmpty(rawToken))
        {
            return AuthProblemTypes.InvalidRefreshToken.ToResult(
                StatusCodes.Status401Unauthorized, httpContext.Request.Path);
        }

        var rotation = await refreshTokenService.ValidateAndRotateAsync(rawToken);
        if (!rotation.Succeeded)
        {
            ClearRefreshTokenCookie(httpContext);
            return AuthProblemTypes.InvalidRefreshToken.ToResult(
                StatusCodes.Status401Unauthorized, httpContext.Request.Path);
        }

        var user = await userManager.FindByIdAsync(rotation.UserId.ToString());
        if (user is null)
        {
            ClearRefreshTokenCookie(httpContext);
            return AuthProblemTypes.InvalidRefreshToken.ToResult(
                StatusCodes.Status401Unauthorized, httpContext.Request.Path);
        }

        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresAtUtc) = jwtTokenService.CreateAccessToken(user.Id, roles);

        SetRefreshTokenCookie(httpContext, rotation.RawToken!, jwtOptions.RefreshTokenLifetimeDays);

        return TypedResults.Ok(new AuthResponse(accessToken, expiresAtUtc, new UserProfile(user.Id, user.Email!, [.. roles])));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext, RefreshTokenService refreshTokenService)
    {
        if (httpContext.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var rawToken) &&
            !string.IsNullOrEmpty(rawToken))
        {
            await refreshTokenService.RevokeAsync(rawToken);
        }

        ClearRefreshTokenCookie(httpContext);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        return TypedResults.Ok(new UserProfile(user.Id, user.Email!, [.. roles]));
    }

    private static void SetRefreshTokenCookie(HttpContext httpContext, string refreshToken, int lifetimeDays)
    {
        httpContext.Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = RefreshTokenCookiePath,
            Expires = DateTimeOffset.UtcNow.AddDays(lifetimeDays),
        });
    }

    private static void ClearRefreshTokenCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = RefreshTokenCookiePath,
        });
    }
}
