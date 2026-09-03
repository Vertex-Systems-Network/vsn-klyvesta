using Klyvesta.Application.Notifications;
using Klyvesta.Domain.Notifications;
using Klyvesta.Infrastructure.Notifications;

var tests = new (string Id, Func<Task> Run)[]
{
    ("NOTIFY-001 provider-neutral delivery succeeds", VerifyDeliveredAsync),
    ("NOTIFY-002 retryable failure is retried", VerifyRetryableThenDeliveredAsync),
    ("NOTIFY-003 permanent failure is not retried", VerifyPermanentFailureAsync),
    ("NOTIFY-004 retry exhaustion becomes terminal", VerifyRetryExhaustedAsync),
    ("NOTIFY-005 identical retry does not resend delivered notification", VerifyIdempotentDeliveredRetryAsync),
    ("NOTIFY-006 changed payload under same key fails closed", VerifyIdempotencyConflictAsync),
    ("NOTIFY-007 idempotency scope separates sources", VerifySourceScopedIdempotencyAsync),
    ("NOTIFY-008 canonical value ordering hashes identically", VerifyCanonicalValueOrderAsync),
    ("NOTIFY-009 changed template value conflicts", VerifyChangedValueConflictAsync),
    ("NOTIFY-010 provider token is stable across attempts", VerifyStableProviderTokenAsync),
    ("NOTIFY-011 delivery ID is stable across attempts", VerifyStableDeliveryIdAsync),
    ("NOTIFY-012 attempt numbers are sequential", VerifyAttemptNumbersAsync),
    ("NOTIFY-013 terminal permanent failure retry does not resend", VerifyTerminalFailureRetryAsync),
    ("NOTIFY-014 missing transport fails before reservation", VerifyMissingTransportAsync),
    ("NOTIFY-015 duplicate transport registration is rejected", VerifyDuplicateTransportRegistrationAsync),
    ("NOTIFY-016 unknown channel is rejected", VerifyUnknownChannelAsync),
    ("NOTIFY-017 retry limit below one is rejected", VerifyLowRetryLimitAsync),
    ("NOTIFY-018 retry limit above five is rejected", VerifyHighRetryLimitAsync),
    ("NOTIFY-019 duplicate template keys are rejected", VerifyDuplicateTemplateKeysAsync),
    ("NOTIFY-020 transport receives rendered payload only", VerifyRenderedBoundaryAsync),
    ("NOTIFY-021 opaque provider exception can resume same delivery", VerifyExceptionResumeAsync),
    ("NOTIFY-022 cancellation fails before reservation", VerifyCancellationAsync),
    ("NOTIFY-023 store contract exposes no delete mutation", VerifyStoreContractAsync),
    ("NOTIFY-024 provider reference and reason evidence are retained", VerifyEvidenceRetentionAsync),
};

var failures = new List<string>();
foreach (var (id, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"NOTIFICATIONS_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"NOTIFICATIONS_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Notification assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("PROVIDER_NEUTRAL: application contracts depend on channel/result abstractions, not vendor SDK types.");
Console.WriteLine("IDEMPOTENT: source + idempotency key binds to a canonical payload hash and terminal retries do not resend.");
Console.WriteLine("RETRY_SEMANTICS: only explicit retryable failures retry; permanent failure and exhaustion are terminal.");
Console.WriteLine("SANITIZED_BOUNDARY: transports receive rendered content plus opaque recipient reference, not raw business objects or template variables.");
Console.WriteLine("NOT_LIVE: verifier transports are scripted in-memory fakes; no email/SMS/push/WhatsApp provider credential or production recipient is used.");

return failures.Count == 0 ? 0 : 1;

static async Task VerifyDeliveredAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Delivered("provider-1"));
    var (dispatcher, store, renderer) = CreateDispatcher(transport);
    var result = await dispatcher.DispatchAsync(Command());
    Require(result.Delivery.State == NotificationDeliveryState.Delivered, "delivery must be terminal delivered");
    Require(result.Delivery.Attempts.Count == 1, "delivery must require one attempt");
    Require(transport.Requests.Count == 1, "transport must be called once");
    Require(renderer.RenderCount == 1, "renderer must run once");
    Require(store.Count == 1, "store must retain one delivery record");
}

static async Task VerifyRetryableThenDeliveredAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Retryable(), Delivered("provider-2"));
    var (dispatcher, _, _) = CreateDispatcher(transport);
    var result = await dispatcher.DispatchAsync(Command(maximumAttempts: 3));
    Require(result.Delivery.State == NotificationDeliveryState.Delivered, "retryable failure must be retried to delivery");
    Require(result.Delivery.Attempts.Count == 2, "two attempts must be retained");
}

static async Task VerifyPermanentFailureAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Permanent());
    var (dispatcher, _, _) = CreateDispatcher(transport);
    var result = await dispatcher.DispatchAsync(Command(maximumAttempts: 5));
    Require(result.Delivery.State == NotificationDeliveryState.PermanentFailure, "permanent failure must be terminal");
    Require(transport.Requests.Count == 1, "permanent failure must not retry");
}

static async Task VerifyRetryExhaustedAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Retryable(), Retryable(), Retryable());
    var (dispatcher, _, _) = CreateDispatcher(transport);
    var result = await dispatcher.DispatchAsync(Command(maximumAttempts: 3));
    Require(result.Delivery.State == NotificationDeliveryState.RetryExhausted, "retry limit must end in retry-exhausted state");
    Require(transport.Requests.Count == 3, "transport calls must equal maximum attempts");
}

static async Task VerifyIdempotentDeliveredRetryAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Delivered("provider-1"));
    var (dispatcher, store, renderer) = CreateDispatcher(transport);
    var first = await dispatcher.DispatchAsync(Command(idempotencyKey: "same-key"));
    var second = await dispatcher.DispatchAsync(Command(idempotencyKey: "same-key"));
    Require(!first.WasExisting, "first dispatch must be new");
    Require(second.WasExisting, "second dispatch must resolve existing terminal delivery");
    Require(second.Delivery.DeliveryId == first.Delivery.DeliveryId, "retry must return same delivery");
    Require(transport.Requests.Count == 1, "terminal retry must not resend");
    Require(renderer.RenderCount == 1, "terminal retry must not rerender");
    Require(store.Count == 1, "terminal retry must not duplicate storage");
}

static async Task VerifyIdempotencyConflictAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Delivered());
    var (dispatcher, store, _) = CreateDispatcher(transport);
    await dispatcher.DispatchAsync(Command(idempotencyKey: "conflict"));
    await RequireThrowsMessageAsync(
        () => dispatcher.DispatchAsync(Command(idempotencyKey: "conflict", templateKey: "security-alert-v2")).AsTask(),
        "NOTIFICATION_IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD");
    Require(store.Count == 1, "conflicting retry must not create a second record");
}

static async Task VerifySourceScopedIdempotencyAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Delivered("a"), Delivered("b"));
    var (dispatcher, store, _) = CreateDispatcher(transport);
    var first = await dispatcher.DispatchAsync(Command(sourceReference: "service-a", idempotencyKey: "same"));
    var second = await dispatcher.DispatchAsync(Command(sourceReference: "service-b", idempotencyKey: "same"));
    Require(first.Delivery.DeliveryId != second.Delivery.DeliveryId, "different sources must have independent idempotency scopes");
    Require(store.Count == 2, "two source scopes must persist independently");
}

static async Task VerifyCanonicalValueOrderAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Delivered());
    var (dispatcher, _, _) = CreateDispatcher(transport);
    var original = Command(idempotencyKey: "ordered") with
    {
        TemplateValues = [new("name", "A"), new("event", "B")],
    };
    var reordered = original with
    {
        TemplateValues = [new("event", "B"), new("name", "A")],
    };
    var first = await dispatcher.DispatchAsync(original);
    var second = await dispatcher.DispatchAsync(reordered);
    Require(second.WasExisting, "template value ordering must not change request identity");
    Require(second.Delivery.DeliveryId == first.Delivery.DeliveryId, "reordered retry must resolve original delivery");
}

static async Task VerifyChangedValueConflictAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Delivered());
    var (dispatcher, _, _) = CreateDispatcher(transport);
    await dispatcher.DispatchAsync(Command(idempotencyKey: "changed-value") with
    {
        TemplateValues = [new("name", "A")],
    });
    await RequireThrowsMessageAsync(
        () => dispatcher.DispatchAsync(Command(idempotencyKey: "changed-value") with
        {
            TemplateValues = [new("name", "B")],
        }).AsTask(),
        "NOTIFICATION_IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD");
}

static async Task VerifyStableProviderTokenAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Retryable(), Delivered());
    var (dispatcher, _, _) = CreateDispatcher(transport);
    await dispatcher.DispatchAsync(Command());
    Require(transport.Requests.Count == 2, "two requests expected");
    Require(transport.Requests[0].ProviderIdempotencyKey == transport.Requests[1].ProviderIdempotencyKey, "provider idempotency token must be stable across attempts");
    Require(transport.Requests[0].ProviderIdempotencyKey.Length == 64, "provider token must be opaque SHA-256 hex");
}

static async Task VerifyStableDeliveryIdAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Retryable(), Delivered());
    var (dispatcher, _, _) = CreateDispatcher(transport);
    await dispatcher.DispatchAsync(Command());
    Require(transport.Requests[0].DeliveryId == transport.Requests[1].DeliveryId, "retry attempts must retain one delivery ID");
}

static async Task VerifyAttemptNumbersAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Retryable(), Retryable(), Delivered());
    var (dispatcher, _, _) = CreateDispatcher(transport);
    var result = await dispatcher.DispatchAsync(Command(maximumAttempts: 3));
    Require(result.Delivery.Attempts.Select(attempt => attempt.AttemptNumber).SequenceEqual([1, 2, 3]), "attempt evidence must be sequential");
}

static async Task VerifyTerminalFailureRetryAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Permanent());
    var (dispatcher, _, _) = CreateDispatcher(transport);
    var first = await dispatcher.DispatchAsync(Command(idempotencyKey: "permanent"));
    var second = await dispatcher.DispatchAsync(Command(idempotencyKey: "permanent"));
    Require(first.Delivery.State == NotificationDeliveryState.PermanentFailure, "first delivery must fail permanently");
    Require(second.WasExisting, "terminal failure retry must resolve stored state");
    Require(transport.Requests.Count == 1, "terminal permanent failure must not resend");
}

static async Task VerifyMissingTransportAsync()
{
    var store = new InMemoryNotificationDeliveryStore();
    var dispatcher = new NotificationDispatcher(new RecordingRenderer(), store, [], new FixedTimeProvider(Now()));
    await RequireThrowsMessageAsync(
        () => dispatcher.DispatchAsync(Command(channel: NotificationChannel.Push)).AsTask(),
        "NOTIFICATION_TRANSPORT_NOT_CONFIGURED");
    Require(store.Count == 0, "missing transport must fail before reservation");
}

static Task VerifyDuplicateTransportRegistrationAsync()
{
    RequireThrows<ArgumentException>(
        () =>
        {
            _ = new NotificationDispatcher(
                new RecordingRenderer(),
                new InMemoryNotificationDeliveryStore(),
                [new ScriptedTransport(NotificationChannel.Email, Delivered()), new ScriptedTransport(NotificationChannel.Email, Delivered())],
                new FixedTimeProvider(Now()));
        },
        "duplicate channel transports must be rejected");
    return Task.CompletedTask;
}

static async Task VerifyUnknownChannelAsync()
{
    var (dispatcher, store, _) = CreateDispatcher(new ScriptedTransport(NotificationChannel.Email, Delivered()));
    await RequireThrowsAsync<ArgumentException>(
        () => dispatcher.DispatchAsync(Command(channel: NotificationChannel.Unknown)).AsTask(),
        "unknown channel must fail");
    Require(store.Count == 0, "invalid channel must not reserve a delivery");
}

static async Task VerifyLowRetryLimitAsync()
{
    var (dispatcher, store, _) = CreateDispatcher(new ScriptedTransport(NotificationChannel.Email, Delivered()));
    await RequireThrowsAsync<ArgumentOutOfRangeException>(
        () => dispatcher.DispatchAsync(Command(maximumAttempts: 0)).AsTask(),
        "zero retry limit must fail");
    Require(store.Count == 0, "invalid retry policy must not reserve");
}

static async Task VerifyHighRetryLimitAsync()
{
    var (dispatcher, store, _) = CreateDispatcher(new ScriptedTransport(NotificationChannel.Email, Delivered()));
    await RequireThrowsAsync<ArgumentOutOfRangeException>(
        () => dispatcher.DispatchAsync(Command(maximumAttempts: 6)).AsTask(),
        "retry limit above five must fail");
    Require(store.Count == 0, "invalid retry policy must not reserve");
}

static async Task VerifyDuplicateTemplateKeysAsync()
{
    var (dispatcher, store, _) = CreateDispatcher(new ScriptedTransport(NotificationChannel.Email, Delivered()));
    var command = Command() with
    {
        TemplateValues = [new("name", "A"), new("name", "B")],
    };
    await RequireThrowsAsync<ArgumentException>(
        () => dispatcher.DispatchAsync(command).AsTask(),
        "duplicate template keys must fail");
    Require(store.Count == 0, "invalid template data must not reserve");
}

static async Task VerifyRenderedBoundaryAsync()
{
    var renderer = new RecordingRenderer();
    var transport = new ScriptedTransport(NotificationChannel.Email, Delivered());
    var store = new InMemoryNotificationDeliveryStore();
    var dispatcher = new NotificationDispatcher(renderer, store, [transport], new FixedTimeProvider(Now()));
    var command = Command() with { TemplateValues = [new("private-template-value", "sanitized-before-transport")] };
    await dispatcher.DispatchAsync(command);
    var request = transport.Requests.Single();
    Require(request.Message.Body == "rendered:security-alert-v1:1", "transport must receive renderer output");
    Require(request.GetType().GetProperty("TemplateValues") is null, "transport request must not expose raw template variables");
}

static async Task VerifyExceptionResumeAsync()
{
    var transport = new ThrowsOnceTransport(NotificationChannel.Email);
    var (dispatcher, store, _) = CreateDispatcher(transport);
    await RequireThrowsAsync<InvalidOperationException>(
        () => dispatcher.DispatchAsync(Command(idempotencyKey: "resume")).AsTask(),
        "opaque provider exception must propagate");
    Require(store.Count == 1, "reservation must persist for safe same-key resume");

    var result = await dispatcher.DispatchAsync(Command(idempotencyKey: "resume"));
    Require(result.WasExisting, "resume must reuse existing delivery reservation");
    Require(result.Delivery.State == NotificationDeliveryState.Delivered, "second dispatch must complete the original delivery");
    Require(transport.Requests[0].DeliveryId == transport.Requests[1].DeliveryId, "resume must retain delivery ID");
    Require(transport.Requests[0].ProviderIdempotencyKey == transport.Requests[1].ProviderIdempotencyKey, "resume must retain provider idempotency token");
    Require(transport.Requests[1].AttemptNumber == 1, "unrecorded ambiguous exception must retry with same attempt ordinal and provider token");
}

static async Task VerifyCancellationAsync()
{
    var transport = new ScriptedTransport(NotificationChannel.Email, Delivered());
    var (dispatcher, store, _) = CreateDispatcher(transport);
    using var source = new CancellationTokenSource();
    source.Cancel();
    await RequireThrowsAsync<OperationCanceledException>(
        () => dispatcher.DispatchAsync(Command(), source.Token).AsTask(),
        "cancelled dispatch must fail");
    Require(store.Count == 0, "pre-cancelled dispatch must not reserve");
    Require(transport.Requests.Count == 0, "pre-cancelled dispatch must not call transport");
}

static Task VerifyStoreContractAsync()
{
    var destructive = typeof(INotificationDeliveryStore).GetMethods()
        .Where(method => method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase))
        .Select(method => method.Name)
        .ToArray();
    Require(destructive.Length == 0, "delivery store contract must not expose destructive delete/remove methods");
    return Task.CompletedTask;
}

static async Task VerifyEvidenceRetentionAsync()
{
    var transport = new ScriptedTransport(
        NotificationChannel.Email,
        new NotificationTransportResult(NotificationAttemptOutcome.Delivered, "PROVIDER_ACCEPTED", "provider-message-42"));
    var (dispatcher, _, _) = CreateDispatcher(transport);
    var result = await dispatcher.DispatchAsync(Command());
    var attempt = result.Delivery.Attempts.Single();
    Require(attempt.ReasonCode == "PROVIDER_ACCEPTED", "reason code must be retained");
    Require(attempt.ProviderReference == "provider-message-42", "provider reference must be retained");
}

static (NotificationDispatcher Dispatcher, InMemoryNotificationDeliveryStore Store, RecordingRenderer Renderer) CreateDispatcher(INotificationTransport transport)
{
    var store = new InMemoryNotificationDeliveryStore();
    var renderer = new RecordingRenderer();
    return (new NotificationDispatcher(renderer, store, [transport], new FixedTimeProvider(Now())), store, renderer);
}

static NotificationDispatchCommand Command(
    string sourceReference = "notification-service",
    string idempotencyKey = "notify-1",
    NotificationChannel channel = NotificationChannel.Email,
    string templateKey = "security-alert-v1",
    int maximumAttempts = 3) =>
    new(
        SourceReference: sourceReference,
        IdempotencyKey: idempotencyKey,
        RecipientReference: "recipient-ref-123",
        Channel: channel,
        TemplateKey: templateKey,
        TemplateValues: [new("display-name", "Customer")],
        MaximumAttempts: maximumAttempts);

static NotificationTransportResult Delivered(string? providerReference = "provider-message") =>
    new(NotificationAttemptOutcome.Delivered, "PROVIDER_ACCEPTED", providerReference);

static NotificationTransportResult Retryable() =>
    new(NotificationAttemptOutcome.RetryableFailure, "PROVIDER_TEMPORARY_FAILURE");

static NotificationTransportResult Permanent() =>
    new(NotificationAttemptOutcome.PermanentFailure, "PROVIDER_PERMANENT_FAILURE");

static DateTimeOffset Now() => new(2026, 9, 3, 13, 30, 0, TimeSpan.Zero);

static async Task RequireThrowsAsync<TException>(Func<Task> action, string message)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static async Task RequireThrowsMessageAsync(Func<Task> action, string expectedMessage)
{
    try
    {
        await action();
    }
    catch (InvalidOperationException exception) when (exception.Message == expectedMessage)
    {
        return;
    }

    throw new InvalidOperationException($"Expected failure {expectedMessage}.");
}

static void RequireThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class RecordingRenderer : INotificationTemplateRenderer
{
    public int RenderCount { get; private set; }

    public RenderedNotification Render(
        string templateKey,
        IReadOnlyCollection<NotificationTemplateValue> values,
        NotificationChannel channel)
    {
        RenderCount++;
        return new RenderedNotification("text/plain", $"rendered:{templateKey}:{values.Count}", $"subject:{channel}");
    }
}

sealed class ScriptedTransport : INotificationTransport
{
    private readonly Queue<NotificationTransportResult> _results;

    public ScriptedTransport(NotificationChannel channel, params NotificationTransportResult[] results)
    {
        Channel = channel;
        _results = new Queue<NotificationTransportResult>(results);
    }

    public NotificationChannel Channel { get; }

    public List<NotificationTransportRequest> Requests { get; } = [];

    public ValueTask<NotificationTransportResult> SendAsync(
        NotificationTransportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        if (_results.Count == 0)
        {
            throw new InvalidOperationException("SCRIPTED_TRANSPORT_RESULT_MISSING");
        }

        return ValueTask.FromResult(_results.Dequeue());
    }
}

sealed class ThrowsOnceTransport(NotificationChannel channel) : INotificationTransport
{
    private bool _hasThrown;

    public NotificationChannel Channel { get; } = channel;

    public List<NotificationTransportRequest> Requests { get; } = [];

    public ValueTask<NotificationTransportResult> SendAsync(
        NotificationTransportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        if (!_hasThrown)
        {
            _hasThrown = true;
            throw new InvalidOperationException("PROVIDER_UNKNOWN_DELIVERY_STATE");
        }

        return ValueTask.FromResult(Delivered("provider-after-resume"));
    }
}
