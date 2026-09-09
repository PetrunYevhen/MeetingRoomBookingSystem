using System.Security.Cryptography;
using System.Text;
using MeetingRoomBooking.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Api.Modules.Auth.Jwt;

public readonly record struct RefreshRotationResult(bool Succeeded, Guid UserId, string? RawToken)
{
    public static RefreshRotationResult Failed() => new(false, default, null);

    public static RefreshRotationResult Success(Guid userId, string rawToken) => new(true, userId, rawToken);
}

/// <summary>
/// Owns the opaque refresh-token lifecycle: issuing a new session on login, rotating it on
/// refresh, and revoking a whole rotation family the moment a revoked token is replayed
/// (ADR 0001's reuse detection). Only the token's hash is ever persisted.
/// </summary>
public sealed class RefreshTokenService(ApplicationDbContext dbContext, JwtOptions options)
{
    public async Task<string> IssueNewSessionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateRawToken();

        dbContext.RefreshTokenSessions.Add(new RefreshTokenSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = Guid.NewGuid(),
            TokenHash = Hash(rawToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(options.RefreshTokenLifetimeDays),
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    public async Task<RefreshRotationResult> ValidateAndRotateAsync(
        string rawToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(rawToken);
        var session = await dbContext.RefreshTokenSessions
            .SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

        if (session is null)
        {
            return RefreshRotationResult.Failed();
        }

        if (session.RevokedAtUtc is not null)
        {
            await RevokeFamilyAsync(session.FamilyId, cancellationToken);
            return RefreshRotationResult.Failed();
        }

        if (session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return RefreshRotationResult.Failed();
        }

        session.RevokedAtUtc = DateTime.UtcNow;

        var newRawToken = GenerateRawToken();
        dbContext.RefreshTokenSessions.Add(new RefreshTokenSession
        {
            Id = Guid.NewGuid(),
            UserId = session.UserId,
            FamilyId = session.FamilyId,
            TokenHash = Hash(newRawToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(options.RefreshTokenLifetimeDays),
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return RefreshRotationResult.Success(session.UserId, newRawToken);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(rawToken);
        var session = await dbContext.RefreshTokenSessions
            .SingleOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAtUtc == null, cancellationToken);

        if (session is null)
        {
            return;
        }

        session.RevokedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var activeFamilySessions = await dbContext.RefreshTokenSessions
            .Where(s => s.FamilyId == familyId && s.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var familySession in activeFamilySessions)
        {
            familySession.RevokedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
