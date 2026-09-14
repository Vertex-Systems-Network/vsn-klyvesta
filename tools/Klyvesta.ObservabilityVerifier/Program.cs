using Klyvesta.Application.Observability;
using Klyvesta.Infrastructure.Observability;

var tests = new (string Id, Func<Task> Run)[]
{
    ("OBS-001 structured event persists canonical fields", VerifyStructuredEventAsync),
    ("OBS-002 trace and span correlation are retained", VerifyCorrelationAsync),
    ("OBS-003 child correlation preserves trace and parent span", VerifyChildCorrelationAsync),
    ("OBS-004 invalid trace identifier is rejected", VerifyInvalidTraceAsync),
    ("OBS-005 invalid span identifier is rejected", VerifyInvalidSpanAsync),
    ("OBS-006 self-parent span is rejected", VerifySelfParentAsync),
    ("OBS-007 password fields are redacted", VerifyPasswordRedactionAsync),
    ("OBS-008 authorization fields are redacted", VerifyAuthorizationRedactionAsync),
    ("OBS-009 token fields are redacted", VerifyTokenKeyRedactionAsync),
    ("OBS-010 bearer values are redacted", VerifyBearerValueRedactionAsync),
    ("OBS-011 basic auth values are redacted", VerifyBasicValueRedactionAsync),
    ("OBS-012 jwt-like values are redacted", VerifyJwtValueRedactionAsync),
    ("OBS-013 GitHub token-like values are redacted", VerifyGithubTokenRedactionAsync),
    ("OBS-014 control characters are scrubbed", VerifyControlScrubbingAsync),
    ("OBS-015 normalized duplicate field keys fail closed", VerifyDuplicateNormalizedKeyAsync),
    ("OBS-016 excessive field count fails closed", VerifyFieldCountLimitAsync),
    ("OBS-017 oversized field values are bounded", VerifyFieldValueBoundAsync),
    ("OBS-018 cancellation prevents sink append", VerifyCancellationAsync),
    ("OBS-019 sink snapshots are isolated", VerifySnapshotIsolationAsync),
    ("OBS-020 empty metadata creates immutable empty map", VerifyEmptyMetadataAsync),
    ("OBS-021 event label control characters are rejected", VerifyEventLabelControlAsync),
    ("OBS-022 unsupported metadata key characters are rejected", VerifyMetadataKeyCharactersAsync),
    ("OBS-023 null metadata values normalize to empty", VerifyNullMetadataValueAsync),
    ("OBS-024 fixed time provider binds deterministic evidence", VerifyDeterministicTimestampAsync),
};

var failures = new List<string>();
foreach (var (id, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"OBSERVABILITY_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"OBSERVABILITY_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Observability assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("STRUCTURED_EVENTS: bounded immutable operational events retain name/category/level/time and sanitized metadata.");
Console.WriteLine("CORRELATION: explicit trace/span boundaries preserve parent-child relationships without ambient mutable global state.");
Console.WriteLine("NO_SECRET_LOGGING: sensitive keys and secret-like values are redacted before reaching the sink.");
Console.WriteLine("NOT_LIVE: verifier uses only an in-memory sink and deterministic synthetic metadata; no production telemetry backend or customer secret is accessed.");

return failures.Count == 0 ? 0 : 1;

static async Task VerifyStructuredEventAsync()
{
    var (writer, sink) = CreateWriter();
    var result = await writer.WriteAsync(
        "order.authorized",
        "orders",
        OperationalEventLevel.Information,
        Correlation(),
        new Dictionary<string, string?> { ["order_id"] = "ord-123" });

    Require(result.Name == "order.authorized", "event name must be retained");
    Require(result.Category == "orders", "event category must be retained");
    Require(result.Level == OperationalEventLevel.Information, "event level must be retained");
    Require(result.Fields["order_id"] == "ord-123", "metadata must be retained");
    Require(sink.Count == 1, "sink must receive exactly one event");
}

static async Task VerifyCorrelationAsync()
{
    var (writer, _) = CreateWriter();
    var correlation = Correlation();
    var result = await writer.WriteAsync("risk.checked", "risk", OperationalEventLevel.Debug, correlation);
    Require(result.Correlation.TraceId == "trace-001", "trace id must survive boundary");
    Require(result.Correlation.SpanId == "span-001", "span id must survive boundary");
    Require(result.Correlation.ParentSpanId is null, "root span must not invent a parent");
}

static Task VerifyChildCorrelationAsync()
{
    var root = Correlation();
    var child = root.CreateChild("span-002");
    Require(child.TraceId == root.TraceId, "child must remain in same trace");
    Require(child.ParentSpanId == root.SpanId, "child must bind explicit parent span");
    Require(child.SpanId == "span-002", "child span id must be explicit");
    return Task.CompletedTask;
}

static Task VerifyInvalidTraceAsync()
{
    RequireThrows<ArgumentException>(() => _ = new CorrelationContext("trace with spaces", "span-1"), "invalid trace must fail");
    return Task.CompletedTask;
}

static Task VerifyInvalidSpanAsync()
{
    RequireThrows<ArgumentException>(() => _ = new CorrelationContext("trace-1", "span/1"), "invalid span must fail");
    return Task.CompletedTask;
}

static Task VerifySelfParentAsync()
{
    RequireThrows<ArgumentException>(() => _ = new CorrelationContext("trace-1", "span-1", "span-1"), "self-parent span must fail");
    return Task.CompletedTask;
}

static async Task VerifyPasswordRedactionAsync()
{
    var result = await WriteSingleFieldAsync("db_password", "super-secret");
    Require(result == "[REDACTED]", "password field must be redacted");
}

static async Task VerifyAuthorizationRedactionAsync()
{
    var result = await WriteSingleFieldAsync("authorization_header", "Bearer hidden");
    Require(result == "[REDACTED]", "authorization field must be redacted");
}

static async Task VerifyTokenKeyRedactionAsync()
{
    var result = await WriteSingleFieldAsync("refresh_token", "abc123");
    Require(result == "[REDACTED]", "token key must be redacted");
}

static async Task VerifyBearerValueRedactionAsync()
{
    var result = await WriteSingleFieldAsync("provider_message", "Bearer abcdefghijklmnopqrstuvwxyz");
    Require(result == "[REDACTED]", "bearer value must be redacted even under safe key");
}

static async Task VerifyBasicValueRedactionAsync()
{
    var result = await WriteSingleFieldAsync("provider_message", "Basic dXNlcjpwYXNz");
    Require(result == "[REDACTED]", "basic auth value must be redacted");
}

static async Task VerifyJwtValueRedactionAsync()
{
    var result = await WriteSingleFieldAsync("provider_message", "eyJhbGciOiJIUzI1NiJ9.payload.signature");
    Require(result == "[REDACTED]", "jwt-like value must be redacted");
}

static async Task VerifyGithubTokenRedactionAsync()
{
    var result = await WriteSingleFieldAsync("provider_message", "github_pat_1234567890abcdefghijklmnopqrstuvwxyz");
    Require(result == "[REDACTED]", "GitHub token-like value must be redacted");
}

static async Task VerifyControlScrubbingAsync()
{
    var result = await WriteSingleFieldAsync("message", "line1\r\nline2\tend");
    Require(!result.Any(char.IsControl), "control characters must not reach sink");
    Require(result.Contains("line1", StringComparison.Ordinal) && result.Contains("line2", StringComparison.Ordinal), "safe content must remain");
}

static async Task VerifyDuplicateNormalizedKeyAsync()
{
    var (writer, sink) = CreateWriter();
    var fields = new Dictionary<string, string?>
    {
        [" Order_Id "] = "one",
        ["order_id"] = "two",
    };

    await RequireThrowsAsync<ArgumentException>(
        () => writer.WriteAsync("event", "category", OperationalEventLevel.Information, Correlation(), fields).AsTask(),
        "normalized duplicate key must fail");
    Require(sink.Count == 0, "invalid metadata must never reach sink");
}

static async Task VerifyFieldCountLimitAsync()
{
    var (writer, sink) = CreateWriter();
    var fields = Enumerable.Range(0, 65).ToDictionary(index => $"field_{index}", index => (string?)index.ToString(System.Globalization.CultureInfo.InvariantCulture));
    await RequireThrowsAsync<ArgumentOutOfRangeException>(
        () => writer.WriteAsync("event", "category", OperationalEventLevel.Information, Correlation(), fields).AsTask(),
        "too many fields must fail");
    Require(sink.Count == 0, "excessive metadata must not append");
}

static async Task VerifyFieldValueBoundAsync()
{
    var result = await WriteSingleFieldAsync("message", new string('x', 1500));
    Require(result.Length == 1024, "metadata values must be bounded to 1024 characters");
}

static async Task VerifyCancellationAsync()
{
    var (writer, sink) = CreateWriter();
    using var source = new CancellationTokenSource();
    source.Cancel();
    await RequireThrowsAsync<OperationCanceledException>(
        () => writer.WriteAsync("event", "category", OperationalEventLevel.Information, Correlation(), cancellationToken: source.Token).AsTask(),
        "cancelled write must fail");
    Require(sink.Count == 0, "cancelled write must not append");
}

static async Task VerifySnapshotIsolationAsync()
{
    var (writer, sink) = CreateWriter();
    await writer.WriteAsync("event.one", "test", OperationalEventLevel.Information, Correlation());
    var snapshot = sink.Snapshot();
    await writer.WriteAsync("event.two", "test", OperationalEventLevel.Information, Correlation().CreateChild("span-002"));
    Require(snapshot.Count == 1, "snapshot must be isolated from subsequent writes");
    Require(sink.Count == 2, "sink must contain both events");
}

static async Task VerifyEmptyMetadataAsync()
{
    var (writer, _) = CreateWriter();
    var result = await writer.WriteAsync("event", "test", OperationalEventLevel.Information, Correlation());
    Require(result.Fields.Count == 0, "missing metadata must become an empty structured map");
    Require(result.Fields is not Dictionary<string, string>, "event must not expose a mutable dictionary");
}

static async Task VerifyEventLabelControlAsync()
{
    var (writer, sink) = CreateWriter();
    await RequireThrowsAsync<ArgumentException>(
        () => writer.WriteAsync("bad\nevent", "test", OperationalEventLevel.Information, Correlation()).AsTask(),
        "control character in event name must fail");
    Require(sink.Count == 0, "invalid event label must not append");
}

static async Task VerifyMetadataKeyCharactersAsync()
{
    var (writer, sink) = CreateWriter();
    var fields = new Dictionary<string, string?> { ["bad/key"] = "value" };
    await RequireThrowsAsync<ArgumentException>(
        () => writer.WriteAsync("event", "test", OperationalEventLevel.Information, Correlation(), fields).AsTask(),
        "unsupported key characters must fail");
    Require(sink.Count == 0, "invalid metadata key must not append");
}

static async Task VerifyNullMetadataValueAsync()
{
    var result = await WriteSingleFieldAsync("optional_value", null);
    Require(result == string.Empty, "null metadata value must normalize to empty string");
}

static async Task VerifyDeterministicTimestampAsync()
{
    var expected = Now();
    var (writer, _) = CreateWriter();
    var result = await writer.WriteAsync("event", "test", OperationalEventLevel.Information, Correlation());
    Require(result.OccurredAt == expected, "time provider must bind deterministic timestamp evidence");
}

static async Task<string> WriteSingleFieldAsync(string key, string? value)
{
    var (writer, _) = CreateWriter();
    var result = await writer.WriteAsync(
        "event",
        "test",
        OperationalEventLevel.Information,
        Correlation(),
        new Dictionary<string, string?> { [key] = value });
    return result.Fields[key.Trim().ToLowerInvariant()];
}

static (OperationalEventWriter Writer, InMemoryOperationalEventSink Sink) CreateWriter()
{
    var sink = new InMemoryOperationalEventSink();
    return (new OperationalEventWriter(sink, new FixedTimeProvider(Now())), sink);
}

static CorrelationContext Correlation() => new("trace-001", "span-001");
static DateTimeOffset Now() => new(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);

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

sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
