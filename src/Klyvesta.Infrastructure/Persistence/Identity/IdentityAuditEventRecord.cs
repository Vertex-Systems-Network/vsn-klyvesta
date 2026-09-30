namespace Klyvesta.Infrastructure.Persistence.Identity;

internal sealed class IdentityAuditEventRecord
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public required string EventType { get; set; }
    public required string Outcome { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? MetadataJson { get; set; }
}