namespace MeetingRoomBooking.Api.Modules.Auth;

public sealed class RefreshTokenSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the opaque token value; the plaintext is never stored.</summary>
    public required string TokenHash { get; set; }

    /// <summary>Shared across a token's whole rotation lineage, so reuse of any revoked
    /// token in the chain can revoke every session descended from it.</summary>
    public Guid FamilyId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
}
