namespace Klyvesta.Infrastructure.Persistence.Records;

internal sealed class WithdrawalBeneficiaryVersionRecord
{
    public Guid VersionId { get; set; }

    public Guid BeneficiaryId { get; set; }

    public int VersionNumber { get; set; }

    public Guid CustomerId { get; set; }

    public required string DestinationHash { get; set; }

    public required string State { get; set; }

    public string? VerificationEvidenceReference { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public DateTimeOffset? AvailableAfter { get; set; }

    public DateTimeOffset? BlockedAt { get; set; }

    public string? BlockReason { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevocationReason { get; set; }
}
