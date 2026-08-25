namespace Klyvesta.Infrastructure.Persistence.Records;

internal sealed class WithdrawalAuthorizationRecord
{
    public Guid Id { get; set; }

    public Guid WithdrawalId { get; set; }

    public Guid PrincipalId { get; set; }

    public Guid SessionId { get; set; }

    public required string TransactionDataHash { get; set; }

    public DateTimeOffset AuthorizedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
