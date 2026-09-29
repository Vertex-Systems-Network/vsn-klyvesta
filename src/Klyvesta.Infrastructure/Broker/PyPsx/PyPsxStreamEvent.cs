namespace Klyvesta.Infrastructure.Broker.PyPsx;

public sealed record PyPsxStreamEvent(
    string EventName,
    string Data,
    DateTimeOffset ObservedAt);
