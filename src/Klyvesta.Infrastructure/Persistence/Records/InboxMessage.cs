namespace Klyvesta.Infrastructure.Persistence.Records;

internal sealed class InboxMessage
{
    public Guid Id { get; set; }

    public required string Provider { get; set; }

    public required string MessageId { get; set; }

    public required string PayloadHash { get; set; }

    public required string PayloadJson { get; set; }

    public required string State { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
