namespace Klyvesta.Infrastructure.Broker.PyPsx;

public enum PyPsxResultState
{
    Success,
    Rejected,
    RetryableFailure,
    Unknown
}

public sealed record PyPsxBrokerResult<T>(
    string RequestId,
    string Operation,
    PyPsxResultState State,
    T? Value,
    int? HttpStatus,
    string? ExternalCorrelationId,
    string? ReasonCode);
