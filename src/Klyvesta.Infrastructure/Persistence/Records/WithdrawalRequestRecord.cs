namespace Klyvesta.Infrastructure.Persistence.Records;

internal sealed class WithdrawalRequestRecord
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Guid BeneficiaryVersionId { get; set; }

    public decimal Amount { get; set; }

    public required string Currency { get; set; }

    public required string DestinationHash { get; set; }

    public required string TransactionDataHash { get; set; }

    public required string State { get; set; }

    public Guid RequestedByPrincipalId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? ReasonCode { get; set; }

    public Guid? ApprovedByPrincipalId { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public Guid? AuthorizationId { get; set; }

    public string? ExternalReference { get; set; }

    public string? OutcomeEvidenceReference { get; set; }
}
