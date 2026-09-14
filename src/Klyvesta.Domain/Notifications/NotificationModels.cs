using System.Collections.ObjectModel;

namespace Klyvesta.Domain.Notifications;

public enum NotificationChannel
{
    Unknown = 0,
    Email = 1,
    Sms = 2,
    Push = 3,
    InApp = 4,
    WhatsApp = 5,
}

public enum NotificationDeliveryState
{
    Unknown = 0,
    Pending = 1,
    Delivered = 2,
    PermanentFailure = 3,
    RetryExhausted = 4,
}

public enum NotificationAttemptOutcome
{
    Unknown = 0,
    Delivered = 1,
    RetryableFailure = 2,
    PermanentFailure = 3,
}

public sealed record NotificationTemplateValue(string Key, string Value)
{
    public NotificationTemplateValue Normalize()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Key);
        ArgumentNullException.ThrowIfNull(Value);

        var key = Key.Trim();
        if (key.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Key), "Template value key is too long.");
        }

        if (Value.Length > 4_000)
        {
            throw new ArgumentOutOfRangeException(nameof(Value), "Template value is too long.");
        }

        return new NotificationTemplateValue(key, Value);
    }
}

public sealed class NotificationDelivery
{
    private readonly ReadOnlyCollection<NotificationAttempt> _attempts;

    public NotificationDelivery(
        Guid deliveryId,
        string sourceReference,
        string idempotencyKey,
        string requestHash,
        string recipientReference,
        NotificationChannel channel,
        string templateKey,
        NotificationDeliveryState state,
        IEnumerable<NotificationAttempt> attempts,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (deliveryId == Guid.Empty)
        {
            throw new ArgumentException("Delivery ID is required.", nameof(deliveryId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentNullException.ThrowIfNull(attempts);
        if (channel == NotificationChannel.Unknown)
        {
            throw new ArgumentException("Notification channel is required.", nameof(channel));
        }

        var materializedAttempts = attempts.ToArray();
        if (materializedAttempts.Select(attempt => attempt.AttemptNumber).Distinct().Count() != materializedAttempts.Length)
        {
            throw new ArgumentException("Notification attempt numbers must be unique.", nameof(attempts));
        }

        DeliveryId = deliveryId;
        SourceReference = sourceReference;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        RecipientReference = recipientReference;
        Channel = channel;
        TemplateKey = templateKey;
        State = state;
        _attempts = Array.AsReadOnly(materializedAttempts);
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid DeliveryId { get; }

    public string SourceReference { get; }

    public string IdempotencyKey { get; }

    public string RequestHash { get; }

    public string RecipientReference { get; }

    public NotificationChannel Channel { get; }

    public string TemplateKey { get; }

    public NotificationDeliveryState State { get; }

    public IReadOnlyList<NotificationAttempt> Attempts => _attempts;

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public bool IsTerminal => State is NotificationDeliveryState.Delivered
        or NotificationDeliveryState.PermanentFailure
        or NotificationDeliveryState.RetryExhausted;
}

public sealed record NotificationAttempt(
    int AttemptNumber,
    NotificationAttemptOutcome Outcome,
    string ReasonCode,
    string? ProviderReference,
    DateTimeOffset AttemptedAt)
{
    public NotificationAttempt Normalize()
    {
        if (AttemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AttemptNumber));
        }

        if (Outcome == NotificationAttemptOutcome.Unknown)
        {
            throw new ArgumentException("Attempt outcome is required.", nameof(Outcome));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ReasonCode);
        return this;
    }
}
