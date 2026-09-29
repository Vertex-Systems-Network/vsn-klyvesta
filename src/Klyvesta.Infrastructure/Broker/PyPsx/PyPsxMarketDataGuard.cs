namespace Klyvesta.Infrastructure.Broker.PyPsx;

public sealed class PyPsxMarketDataGuard(TimeSpan maxAge)
{
    public bool IsFresh(DateTimeOffset sourceTimestamp, DateTimeOffset? now = null)
        => sourceTimestamp >= (now ?? DateTimeOffset.UtcNow) - maxAge;
}

public sealed class PyPsxReplayWindow(TimeSpan retention)
{
    private readonly Dictionary<string, DateTimeOffset> seen = new(StringComparer.Ordinal);
    private readonly object sync = new();

    public bool TryRegister(string idempotencyKey, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        }

        var current = now ?? DateTimeOffset.UtcNow;
        lock (sync)
        {
            foreach (var expired in seen.Where(item => item.Value <= current - retention).Select(item => item.Key).ToArray())
            {
                seen.Remove(expired);
            }

            if (seen.ContainsKey(idempotencyKey))
            {
                return false;
            }

            seen[idempotencyKey] = current;
            return true;
        }
    }
}
