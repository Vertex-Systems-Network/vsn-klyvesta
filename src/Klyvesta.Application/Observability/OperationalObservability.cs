using System.Collections.ObjectModel;
using System.Text;

namespace Klyvesta.Application.Observability;

public enum OperationalEventLevel
{
    Trace = 1,
    Debug = 2,
    Information = 3,
    Warning = 4,
    Error = 5,
    Critical = 6,
}

public sealed record CorrelationContext
{
    public CorrelationContext(string traceId, string spanId, string? parentSpanId = null)
    {
        TraceId = ValidateIdentifier(traceId, nameof(traceId));
        SpanId = ValidateIdentifier(spanId, nameof(spanId));
        ParentSpanId = parentSpanId is null ? null : ValidateIdentifier(parentSpanId, nameof(parentSpanId));

        if (ParentSpanId is not null && StringComparer.Ordinal.Equals(ParentSpanId, SpanId))
        {
            throw new ArgumentException("Parent span must differ from the current span.", nameof(parentSpanId));
        }
    }

    public string TraceId { get; }

    public string SpanId { get; }

    public string? ParentSpanId { get; }

    public CorrelationContext CreateChild(string childSpanId) => new(TraceId, childSpanId, SpanId);

    private static string ValidateIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > 128)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Correlation identifiers may not exceed 128 characters.");
        }

        if (normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':')))
        {
            throw new ArgumentException("Correlation identifiers contain unsupported characters.", parameterName);
        }

        return normalized;
    }
}

public sealed record OperationalEvent
{
    public OperationalEvent(
        string name,
        string category,
        OperationalEventLevel level,
        CorrelationContext correlation,
        DateTimeOffset occurredAt,
        ReadOnlyDictionary<string, string> fields)
    {
        Name = ValidateLabel(name, nameof(name));
        Category = ValidateLabel(category, nameof(category));
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        Correlation = correlation ?? throw new ArgumentNullException(nameof(correlation));
        OccurredAt = occurredAt;
        Fields = fields ?? throw new ArgumentNullException(nameof(fields));
    }

    public string Name { get; }

    public string Category { get; }

    public OperationalEventLevel Level { get; }

    public CorrelationContext Correlation { get; }

    public DateTimeOffset OccurredAt { get; }

    public ReadOnlyDictionary<string, string> Fields { get; }

    private static string ValidateLabel(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > 128)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Structured event labels may not exceed 128 characters.");
        }

        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Structured event labels may not contain control characters.", parameterName);
        }

        return normalized;
    }
}

public interface IOperationalEventSink
{
    ValueTask WriteAsync(OperationalEvent operationalEvent, CancellationToken cancellationToken = default);
}

public sealed class OperationalEventWriter
{
    private const string RedactedValue = "[REDACTED]";
    private readonly IOperationalEventSink _sink;
    private readonly TimeProvider _timeProvider;

    public OperationalEventWriter(IOperationalEventSink sink, TimeProvider timeProvider)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<OperationalEvent> WriteAsync(
        string name,
        string category,
        OperationalEventLevel level,
        CorrelationContext correlation,
        IReadOnlyDictionary<string, string?>? fields = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(correlation);

        var sanitizedFields = SanitizeFields(fields);
        var operationalEvent = new OperationalEvent(
            name,
            category,
            level,
            correlation,
            _timeProvider.GetUtcNow(),
            sanitizedFields);

        await _sink.WriteAsync(operationalEvent, cancellationToken).ConfigureAwait(false);
        return operationalEvent;
    }

    private static ReadOnlyDictionary<string, string> SanitizeFields(IReadOnlyDictionary<string, string?>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));
        }

        if (fields.Count > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(fields), "Structured events may contain at most 64 metadata fields.");
        }

        var sanitized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in fields)
        {
            var key = NormalizeFieldKey(pair.Key);
            if (!sanitized.TryAdd(key, SanitizeValue(key, pair.Value)))
            {
                throw new ArgumentException("Structured event field keys must remain unique after normalization.", nameof(fields));
            }
        }

        return new ReadOnlyDictionary<string, string>(sanitized);
    }

    private static string NormalizeFieldKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.Length > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(key), "Structured event field keys may not exceed 64 characters.");
        }

        if (normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-' or '.')))
        {
            throw new ArgumentException("Structured event field keys contain unsupported characters.", nameof(key));
        }

        return normalized;
    }

    private static string SanitizeValue(string normalizedKey, string? value)
    {
        if (IsSensitiveKey(normalizedKey))
        {
            return RedactedValue;
        }

        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var cleaned = RemoveControlCharacters(value.Trim());
        if (LooksSensitiveValue(cleaned))
        {
            return RedactedValue;
        }

        return cleaned.Length <= 1024 ? cleaned : cleaned[..1024];
    }

    private static bool IsSensitiveKey(string normalizedKey) =>
        normalizedKey.Contains("password", StringComparison.Ordinal)
        || normalizedKey.Contains("passwd", StringComparison.Ordinal)
        || normalizedKey.Contains("secret", StringComparison.Ordinal)
        || normalizedKey.Contains("token", StringComparison.Ordinal)
        || normalizedKey.Contains("authorization", StringComparison.Ordinal)
        || normalizedKey.Contains("cookie", StringComparison.Ordinal)
        || normalizedKey.Contains("credential", StringComparison.Ordinal)
        || normalizedKey.Contains("api_key", StringComparison.Ordinal)
        || normalizedKey.Contains("apikey", StringComparison.Ordinal)
        || normalizedKey.Contains("private_key", StringComparison.Ordinal)
        || normalizedKey.Contains("access_key", StringComparison.Ordinal);

    private static bool LooksSensitiveValue(string value)
    {
        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("sk-", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("ghp_", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("github_pat_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Length < 24)
        {
            return false;
        }

        var firstDot = value.IndexOf('.', StringComparison.Ordinal);
        if (firstDot <= 0)
        {
            return false;
        }

        var secondDot = value.IndexOf('.', firstDot + 1);
        return secondDot > firstDot + 1 && secondDot < value.Length - 1;
    }

    private static string RemoveControlCharacters(string value)
    {
        if (!value.Any(char.IsControl))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.ToString();
    }
}
