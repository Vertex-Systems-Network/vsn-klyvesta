namespace Klyvesta.Infrastructure.Persistence.Records;

internal sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public required string EventType { get; set; }

    public required string PayloadJson { get; set; }

    public string? HeadersJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? LastErrorCode { get; set; }
}
