using Klyvesta.Application.Observability;

namespace Klyvesta.Infrastructure.Observability;

public sealed class InMemoryOperationalEventSink : IOperationalEventSink
{
    private readonly object _gate = new();
    private readonly List<OperationalEvent> _events = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _events.Count;
            }
        }
    }

    public ValueTask WriteAsync(OperationalEvent operationalEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operationalEvent);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _events.Add(operationalEvent);
        }

        return ValueTask.CompletedTask;
    }

    public IReadOnlyList<OperationalEvent> Snapshot()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }
}
