namespace Klyvesta.Infrastructure.Persistence.Records;

internal sealed class SecurityDeviceRecord
{
    public Guid Id { get; set; }

    public Guid PrincipalId { get; set; }

    public required string PrincipalType { get; set; }

    public required string TrustState { get; set; }

    public required string IntegrityState { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? RestrictedAt { get; set; }

    public string? RestrictionReason { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevocationReason { get; set; }
}
