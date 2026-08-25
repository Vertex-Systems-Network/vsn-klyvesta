namespace Klyvesta.Infrastructure.Persistence.Records;

internal sealed class SecuritySessionRecord
{
    public Guid Id { get; set; }

    public Guid PrincipalId { get; set; }

    public required string PrincipalType { get; set; }

    public Guid DeviceId { get; set; }

    public DateTimeOffset AuthenticatedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public int IdleTimeoutSeconds { get; set; }

    public DateTimeOffset AbsoluteExpiresAt { get; set; }

    public required string AuthenticationStrength { get; set; }

    public bool Restricted { get; set; }

    public DateTimeOffset? RestrictedAt { get; set; }

    public string? RestrictionReason { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevocationReason { get; set; }
}
