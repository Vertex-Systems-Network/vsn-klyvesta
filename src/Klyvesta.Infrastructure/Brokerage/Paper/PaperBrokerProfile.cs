using Klyvesta.Application.Brokerage;

namespace Klyvesta.Infrastructure.Brokerage.Paper;

public enum PaperSubmissionOutcome
{
    Open,
    FullFill,
    PartialFill,
    Rejected,
}

public sealed record PaperBrokerProfile
{
    public PaperSubmissionOutcome SubmissionOutcome { get; init; } = PaperSubmissionOutcome.FullFill;

    public decimal FillPrice { get; init; } = 100m;

    public decimal PartialFillRatio { get; init; } = 0.4m;

    public int FillCount { get; init; } = 1;

    public decimal StartingCash { get; init; } = 1_000_000m;

    public bool IsAvailable { get; init; } = true;

    public bool IsRateLimited { get; init; }

    public bool TimeoutBeforeSideEffect { get; init; }

    public bool AmbiguousTimeoutAfterAcceptance { get; init; }

    public bool MarketOpen { get; init; } = true;

    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UnixEpoch;
}

public sealed record PaperBrokerEvent(
    string EventId,
    Guid BrokerOrderId,
    BrokerOrderState State,
    BrokerExecution? Execution = null);

public sealed record PaperBrokerEventResult(
    bool DuplicateEvent,
    bool DuplicateExecution,
    BrokerOrderSnapshot Snapshot);
