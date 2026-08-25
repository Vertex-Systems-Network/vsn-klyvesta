namespace Klyvesta.Infrastructure.Persistence.Records;

internal sealed class IdempotencyRecord
{
    public Guid Id { get; set; }

    public required string Scope { get; set; }

    public required string Key { get; set; }

    public required string RequestHash { get; set; }

    public required string State { get; set; }

    public Guid? OperationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
