namespace Klyvesta.Infrastructure.Broker.PyPsx;

public static class PyPsxReliabilityPolicy
{
    public static bool IsRetryable(PyPsxBrokerResult<object?> result)
        => result.State == PyPsxResultState.RetryableFailure
           && result.HttpStatus is >= 500 or 429;

    public static bool IsAmbiguous(PyPsxBrokerResult<object?> result)
        => result.State == PyPsxResultState.Unknown
           || result.ReasonCode is "TIMEOUT_OR_AMBIGUOUS" or "NETWORK_FAILURE";

    public static bool CanReplayRead(PyPsxBrokerResult<object?> result)
        => IsRetryable(result) || IsAmbiguous(result);

    public static bool CanReplaySubmission(PyPsxBrokerResult<object?> result)
        => IsRetryable(result) && result.ExternalCorrelationId is not null;
}
