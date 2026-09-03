using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Klyvesta.Domain.Notifications;

namespace Klyvesta.Application.Notifications;

public sealed record NotificationDispatchCommand(
    string SourceReference,
    string IdempotencyKey,
    string RecipientReference,
    NotificationChannel Channel,
    string TemplateKey,
    IReadOnlyCollection<NotificationTemplateValue> TemplateValues,
    int MaximumAttempts = 3);

public sealed record RenderedNotification(string ContentType, string Body, string? Subject = null);

public sealed record NotificationTransportRequest(
    Guid DeliveryId,
    string ProviderIdempotencyKey,
    string RecipientReference,
    NotificationChannel Channel,
    RenderedNotification Message,
    int AttemptNumber);

public sealed record NotificationTransportResult(
    NotificationAttemptOutcome Outcome,
    string ReasonCode,
    string? ProviderReference = null);

public sealed record NotificationDispatchResult(NotificationDelivery Delivery, bool WasExisting);

public sealed record NotificationDeliverySeed(
    string SourceReference,
    string IdempotencyKey,
    string RequestHash,
    string RecipientReference,
    NotificationChannel Channel,
    string TemplateKey,
    DateTimeOffset CreatedAt);

public sealed record NotificationReservation(NotificationDelivery Delivery, bool WasExisting);

public interface INotificationTemplateRenderer
{
    RenderedNotification Render(
        string templateKey,
        IReadOnlyCollection<NotificationTemplateValue> values,
        NotificationChannel channel);
}

public interface INotificationTransport
{
    NotificationChannel Channel { get; }

    ValueTask<NotificationTransportResult> SendAsync(
        NotificationTransportRequest request,
        CancellationToken cancellationToken = default);
}

public interface INotificationDeliveryStore
{
    ValueTask<NotificationReservation> ReserveAsync(
        NotificationDeliverySeed seed,
        CancellationToken cancellationToken = default);

    ValueTask<NotificationDelivery> RecordAttemptAsync(
        Guid deliveryId,
        NotificationAttempt attempt,
        NotificationDeliveryState resultingState,
        CancellationToken cancellationToken = default);
}

public sealed class NotificationDispatcher
{
    private readonly INotificationTemplateRenderer _renderer;
    private readonly INotificationDeliveryStore _store;
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationTransport> _transports;
    private readonly TimeProvider _timeProvider;

    public NotificationDispatcher(
        INotificationTemplateRenderer renderer,
        INotificationDeliveryStore store,
        IEnumerable<INotificationTransport> transports,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(transports);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _renderer = renderer;
        _store = store;
        _timeProvider = timeProvider;

        var transportArray = transports.ToArray();
        if (transportArray.Any(transport => transport.Channel == NotificationChannel.Unknown))
        {
            throw new ArgumentException("Notification transports must declare a concrete channel.", nameof(transports));
        }

        if (transportArray.GroupBy(transport => transport.Channel).Any(group => group.Count() != 1))
        {
            throw new ArgumentException("Only one transport may be registered per notification channel.", nameof(transports));
        }

        _transports = transportArray.ToDictionary(transport => transport.Channel);
    }

    public async ValueTask<NotificationDispatchResult> DispatchAsync(
        NotificationDispatchCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalizedValues = ValidateAndNormalize(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_transports.TryGetValue(command.Channel, out var transport))
        {
            throw new InvalidOperationException("NOTIFICATION_TRANSPORT_NOT_CONFIGURED");
        }

        var requestHash = NotificationRequestHasher.Compute(command, normalizedValues);
        var reservation = await _store.ReserveAsync(
            new NotificationDeliverySeed(
                command.SourceReference.Trim(),
                command.IdempotencyKey.Trim(),
                requestHash,
                command.RecipientReference.Trim(),
                command.Channel,
                command.TemplateKey.Trim(),
                _timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        if (reservation.Delivery.IsTerminal)
        {
            return new NotificationDispatchResult(reservation.Delivery, WasExisting: true);
        }

        var rendered = _renderer.Render(command.TemplateKey.Trim(), normalizedValues, command.Channel);
        ValidateRendered(rendered);

        var current = reservation.Delivery;
        var providerIdempotencyKey = NotificationRequestHasher.ProviderToken(
            current.SourceReference,
            current.IdempotencyKey,
            current.DeliveryId);

        for (var attemptNumber = current.Attempts.Count + 1; attemptNumber <= command.MaximumAttempts; attemptNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await transport.SendAsync(
                new NotificationTransportRequest(
                    current.DeliveryId,
                    providerIdempotencyKey,
                    current.RecipientReference,
                    current.Channel,
                    rendered,
                    attemptNumber),
                cancellationToken).ConfigureAwait(false);
            ValidateTransportResult(result);

            var state = result.Outcome switch
            {
                NotificationAttemptOutcome.Delivered => NotificationDeliveryState.Delivered,
                NotificationAttemptOutcome.PermanentFailure => NotificationDeliveryState.PermanentFailure,
                NotificationAttemptOutcome.RetryableFailure when attemptNumber >= command.MaximumAttempts => NotificationDeliveryState.RetryExhausted,
                NotificationAttemptOutcome.RetryableFailure => NotificationDeliveryState.Pending,
                _ => throw new InvalidOperationException("NOTIFICATION_TRANSPORT_OUTCOME_INVALID"),
            };

            current = await _store.RecordAttemptAsync(
                current.DeliveryId,
                new NotificationAttempt(
                    attemptNumber,
                    result.Outcome,
                    result.ReasonCode,
                    result.ProviderReference,
                    _timeProvider.GetUtcNow()),
                state,
                cancellationToken).ConfigureAwait(false);

            if (current.IsTerminal)
            {
                return new NotificationDispatchResult(current, reservation.WasExisting);
            }
        }

        throw new InvalidOperationException("NOTIFICATION_RETRY_STATE_INVALID");
    }

    private static IReadOnlyCollection<NotificationTemplateValue> ValidateAndNormalize(NotificationDispatchCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.SourceReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.IdempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.RecipientReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.TemplateKey);
        ArgumentNullException.ThrowIfNull(command.TemplateValues);

        if (command.Channel == NotificationChannel.Unknown)
        {
            throw new ArgumentException("Notification channel is required.", nameof(command));
        }

        if (command.MaximumAttempts is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Maximum attempts must be between one and five.");
        }

        var normalized = command.TemplateValues.Select(value => value.Normalize()).ToArray();
        if (normalized.GroupBy(value => value.Key, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            throw new ArgumentException("Template value keys must be unique.", nameof(command));
        }

        return Array.AsReadOnly(normalized);
    }

    private static void ValidateRendered(RenderedNotification rendered)
    {
        ArgumentNullException.ThrowIfNull(rendered);
        ArgumentException.ThrowIfNullOrWhiteSpace(rendered.ContentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(rendered.Body);
        if (rendered.Body.Length > 20_000)
        {
            throw new InvalidOperationException("NOTIFICATION_RENDERED_BODY_TOO_LARGE");
        }
    }

    private static void ValidateTransportResult(NotificationTransportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Outcome == NotificationAttemptOutcome.Unknown)
        {
            throw new InvalidOperationException("NOTIFICATION_TRANSPORT_OUTCOME_INVALID");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(result.ReasonCode);
    }
}

internal static class NotificationRequestHasher
{
    public static string Compute(
        NotificationDispatchCommand command,
        IReadOnlyCollection<NotificationTemplateValue> normalizedValues)
    {
        var builder = new StringBuilder();
        builder.Append(command.RecipientReference.Trim()).Append('|');
        builder.Append(((int)command.Channel).ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(command.TemplateKey.Trim());

        foreach (var value in normalizedValues.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            builder.Append('|').Append(value.Key).Append('=').Append(value.Value);
        }

        return Hash(builder.ToString());
    }

    public static string ProviderToken(string sourceReference, string idempotencyKey, Guid deliveryId) =>
        Hash($"{sourceReference}|{idempotencyKey}|{deliveryId:D}");

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
