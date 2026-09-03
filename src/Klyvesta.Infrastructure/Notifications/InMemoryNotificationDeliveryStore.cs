using Klyvesta.Application.Notifications;
using Klyvesta.Domain.Notifications;

namespace Klyvesta.Infrastructure.Notifications;

public sealed class InMemoryNotificationDeliveryStore : INotificationDeliveryStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, StoredDelivery> _deliveries = [];
    private readonly Dictionary<(string Source, string Key), Guid> _idempotency = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _deliveries.Count;
            }
        }
    }

    public ValueTask<NotificationReservation> ReserveAsync(
        NotificationDeliverySeed seed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seed);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var scope = (seed.SourceReference, seed.IdempotencyKey);
            if (_idempotency.TryGetValue(scope, out var existingId))
            {
                var existing = _deliveries[existingId];
                if (!StringComparer.Ordinal.Equals(existing.RequestHash, seed.RequestHash))
                {
                    throw new InvalidOperationException("NOTIFICATION_IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD");
                }

                return ValueTask.FromResult(new NotificationReservation(existing.Snapshot(), WasExisting: true));
            }

            var id = Guid.CreateVersion7();
            var stored = new StoredDelivery(seed, id);
            _deliveries.Add(id, stored);
            _idempotency.Add(scope, id);
            return ValueTask.FromResult(new NotificationReservation(stored.Snapshot(), WasExisting: false));
        }
    }

    public ValueTask<NotificationDelivery> RecordAttemptAsync(
        Guid deliveryId,
        NotificationAttempt attempt,
        NotificationDeliveryState resultingState,
        CancellationToken cancellationToken = default)
    {
        if (deliveryId == Guid.Empty)
        {
            throw new ArgumentException("Delivery ID is required.", nameof(deliveryId));
        }

        ArgumentNullException.ThrowIfNull(attempt);
        attempt.Normalize();
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_deliveries.TryGetValue(deliveryId, out var stored))
            {
                throw new InvalidOperationException("NOTIFICATION_DELIVERY_NOT_FOUND");
            }

            if (stored.State != NotificationDeliveryState.Pending)
            {
                throw new InvalidOperationException("NOTIFICATION_DELIVERY_ALREADY_TERMINAL");
            }

            if (attempt.AttemptNumber != stored.Attempts.Count + 1)
            {
                throw new InvalidOperationException("NOTIFICATION_ATTEMPT_SEQUENCE_INVALID");
            }

            ValidateTransition(attempt.Outcome, resultingState);
            stored.Attempts.Add(attempt);
            stored.State = resultingState;
            stored.UpdatedAt = attempt.AttemptedAt;
            return ValueTask.FromResult(stored.Snapshot());
        }
    }

    private static void ValidateTransition(
        NotificationAttemptOutcome outcome,
        NotificationDeliveryState resultingState)
    {
        var valid = outcome switch
        {
            NotificationAttemptOutcome.Delivered => resultingState == NotificationDeliveryState.Delivered,
            NotificationAttemptOutcome.PermanentFailure => resultingState == NotificationDeliveryState.PermanentFailure,
            NotificationAttemptOutcome.RetryableFailure => resultingState is NotificationDeliveryState.Pending or NotificationDeliveryState.RetryExhausted,
            _ => false,
        };

        if (!valid)
        {
            throw new InvalidOperationException("NOTIFICATION_DELIVERY_TRANSITION_INVALID");
        }
    }

    private sealed class StoredDelivery
    {
        public StoredDelivery(NotificationDeliverySeed seed, Guid deliveryId)
        {
            DeliveryId = deliveryId;
            SourceReference = seed.SourceReference;
            IdempotencyKey = seed.IdempotencyKey;
            RequestHash = seed.RequestHash;
            RecipientReference = seed.RecipientReference;
            Channel = seed.Channel;
            TemplateKey = seed.TemplateKey;
            State = NotificationDeliveryState.Pending;
            CreatedAt = seed.CreatedAt;
            UpdatedAt = seed.CreatedAt;
        }

        public Guid DeliveryId { get; }

        public string SourceReference { get; }

        public string IdempotencyKey { get; }

        public string RequestHash { get; }

        public string RecipientReference { get; }

        public NotificationChannel Channel { get; }

        public string TemplateKey { get; }

        public NotificationDeliveryState State { get; set; }

        public List<NotificationAttempt> Attempts { get; } = [];

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset UpdatedAt { get; set; }

        public NotificationDelivery Snapshot() =>
            new(
                DeliveryId,
                SourceReference,
                IdempotencyKey,
                RequestHash,
                RecipientReference,
                Channel,
                TemplateKey,
                State,
                Attempts,
                CreatedAt,
                UpdatedAt);
    }
}
