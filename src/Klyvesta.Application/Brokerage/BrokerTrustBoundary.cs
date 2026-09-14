namespace Klyvesta.Application.Brokerage;

public sealed record BrokerEventEnvelope(
    string EventId,
    string Nonce,
    DateTimeOffset SentAt,
    string PayloadDigest,
    string Signature,
    string KeyId);

public interface IBrokerEventSignatureVerifier
{
    bool Verify(BrokerEventEnvelope envelope);
}

public enum BrokerTrustOutcome
{
    Reject = 0,
    Accept = 1,
}

public sealed record BrokerTrustDecision(BrokerTrustOutcome Outcome, string ReasonCode)
{
    public bool IsAccepted => Outcome == BrokerTrustOutcome.Accept;
}

public sealed class BrokerEventTrustGate
{
    private readonly object _sync = new();
    private readonly IBrokerEventSignatureVerifier _signatureVerifier;
    private readonly TimeSpan _maxAge;
    private readonly TimeSpan _maxFutureSkew;
    private readonly HashSet<string> _acceptedEventIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _acceptedNonces = new(StringComparer.Ordinal);

    public BrokerEventTrustGate(
        IBrokerEventSignatureVerifier signatureVerifier,
        TimeSpan maxAge,
        TimeSpan maxFutureSkew)
    {
        ArgumentNullException.ThrowIfNull(signatureVerifier);
        if (maxAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAge));
        }

        if (maxFutureSkew < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFutureSkew));
        }

        _signatureVerifier = signatureVerifier;
        _maxAge = maxAge;
        _maxFutureSkew = maxFutureSkew;
    }

    public BrokerTrustDecision Evaluate(BrokerEventEnvelope envelope, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (string.IsNullOrWhiteSpace(envelope.EventId) ||
            string.IsNullOrWhiteSpace(envelope.Nonce) ||
            string.IsNullOrWhiteSpace(envelope.PayloadDigest) ||
            string.IsNullOrWhiteSpace(envelope.Signature) ||
            string.IsNullOrWhiteSpace(envelope.KeyId))
        {
            return Reject("BROKER_EVENT_ENVELOPE_INVALID");
        }

        if (envelope.SentAt > now + _maxFutureSkew)
        {
            return Reject("BROKER_EVENT_FROM_FUTURE");
        }

        if (now - envelope.SentAt > _maxAge)
        {
            return Reject("BROKER_EVENT_STALE");
        }

        if (!_signatureVerifier.Verify(envelope))
        {
            return Reject("BROKER_EVENT_SIGNATURE_INVALID");
        }

        lock (_sync)
        {
            if (_acceptedEventIds.Contains(envelope.EventId))
            {
                return Reject("BROKER_EVENT_REPLAY_EVENT_ID");
            }

            if (_acceptedNonces.Contains(envelope.Nonce))
            {
                return Reject("BROKER_EVENT_REPLAY_NONCE");
            }

            _acceptedEventIds.Add(envelope.EventId);
            _acceptedNonces.Add(envelope.Nonce);
        }

        return new BrokerTrustDecision(BrokerTrustOutcome.Accept, "BROKER_EVENT_ACCEPTED");
    }

    private static BrokerTrustDecision Reject(string reasonCode) =>
        new(BrokerTrustOutcome.Reject, reasonCode);
}

public sealed record MarketDataQuote(
    string InstrumentReference,
    string SourceReference,
    decimal Price,
    DateTimeOffset ObservedAt);

public sealed record MarketDataTrustPolicy(
    TimeSpan MaxAge,
    TimeSpan MaxFutureSkew,
    decimal MaxReferenceDeviationFraction)
{
    public void Validate()
    {
        if (MaxAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAge));
        }

        if (MaxFutureSkew < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFutureSkew));
        }

        if (MaxReferenceDeviationFraction < 0m || MaxReferenceDeviationFraction > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxReferenceDeviationFraction));
        }
    }
}

public sealed class MarketDataTrustGate
{
    private readonly MarketDataTrustPolicy _policy;

    public MarketDataTrustGate(MarketDataTrustPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        _policy = policy;
    }

    public BrokerTrustDecision Evaluate(
        MarketDataQuote quote,
        decimal trustedReferencePrice,
        bool marketSessionOpen,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(quote);

        if (string.IsNullOrWhiteSpace(quote.InstrumentReference) ||
            string.IsNullOrWhiteSpace(quote.SourceReference) ||
            quote.Price <= 0m ||
            trustedReferencePrice <= 0m)
        {
            return Reject("MARKET_DATA_INVALID");
        }

        if (!marketSessionOpen)
        {
            return Reject("MARKET_SESSION_CLOSED");
        }

        if (quote.ObservedAt > now + _policy.MaxFutureSkew)
        {
            return Reject("MARKET_DATA_FROM_FUTURE");
        }

        if (now - quote.ObservedAt > _policy.MaxAge)
        {
            return Reject("MARKET_DATA_STALE");
        }

        var deviation = decimal.Abs(quote.Price - trustedReferencePrice) / trustedReferencePrice;
        if (deviation > _policy.MaxReferenceDeviationFraction)
        {
            return Reject("MARKET_DATA_DEVIATION_EXCEEDED");
        }

        return new BrokerTrustDecision(BrokerTrustOutcome.Accept, "MARKET_DATA_ACCEPTED");
    }

    private static BrokerTrustDecision Reject(string reasonCode) =>
        new(BrokerTrustOutcome.Reject, reasonCode);
}

public sealed record BrokerExecutionSafetyDecision(bool IsAllowed, string ReasonCode);

public sealed record BrokerDependencySafetySnapshot(
    bool GlobalAutoPaused,
    IReadOnlySet<string> PausedAccounts,
    int ConsecutiveCriticalFailures,
    string? GlobalPauseReason);

public sealed class BrokerDependencySafetyGate
{
    private readonly object _sync = new();
    private readonly int _criticalFailureThreshold;
    private readonly HashSet<string> _pausedAccounts = new(StringComparer.Ordinal);
    private int _consecutiveCriticalFailures;
    private bool _globalAutoPaused;
    private string? _globalPauseReason;

    public BrokerDependencySafetyGate(int criticalFailureThreshold = 3)
    {
        if (criticalFailureThreshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(criticalFailureThreshold));
        }

        _criticalFailureThreshold = criticalFailureThreshold;
    }

    public BrokerDependencySafetySnapshot RecordCriticalDependencyFailure(string reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new ArgumentException("Reason code is required.", nameof(reasonCode));
        }

        lock (_sync)
        {
            _consecutiveCriticalFailures++;
            if (_consecutiveCriticalFailures >= _criticalFailureThreshold)
            {
                _globalAutoPaused = true;
                _globalPauseReason = reasonCode;
            }

            return SnapshotLocked();
        }
    }

    public BrokerDependencySafetySnapshot RecordHealthyDependencyEvidence()
    {
        lock (_sync)
        {
            _consecutiveCriticalFailures = 0;
            return SnapshotLocked();
        }
    }

    public BrokerDependencySafetySnapshot PauseAccount(string accountReference)
    {
        ValidateAccountReference(accountReference);
        lock (_sync)
        {
            _pausedAccounts.Add(accountReference);
            return SnapshotLocked();
        }
    }

    public BrokerDependencySafetySnapshot ClearAccountPauseAfterVerifiedReconciliation(string accountReference)
    {
        ValidateAccountReference(accountReference);
        lock (_sync)
        {
            _pausedAccounts.Remove(accountReference);
            return SnapshotLocked();
        }
    }

    public BrokerDependencySafetySnapshot ClearGlobalPauseAfterVerifiedRecovery()
    {
        lock (_sync)
        {
            if (_consecutiveCriticalFailures != 0)
            {
                throw new InvalidOperationException("Global broker pause cannot clear before healthy dependency evidence resets the failure count.");
            }

            _globalAutoPaused = false;
            _globalPauseReason = null;
            return SnapshotLocked();
        }
    }

    public BrokerExecutionSafetyDecision EvaluateExecution(string accountReference)
    {
        ValidateAccountReference(accountReference);
        lock (_sync)
        {
            if (_globalAutoPaused)
            {
                return new BrokerExecutionSafetyDecision(false, _globalPauseReason ?? "BROKER_DEPENDENCY_GLOBAL_PAUSE");
            }

            if (_pausedAccounts.Contains(accountReference))
            {
                return new BrokerExecutionSafetyDecision(false, "BROKER_DEPENDENCY_ACCOUNT_PAUSE");
            }

            return new BrokerExecutionSafetyDecision(true, "BROKER_DEPENDENCY_EXECUTION_ALLOWED");
        }
    }

    public BrokerDependencySafetySnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return SnapshotLocked();
        }
    }

    private BrokerDependencySafetySnapshot SnapshotLocked() =>
        new(
            _globalAutoPaused,
            new HashSet<string>(_pausedAccounts, StringComparer.Ordinal),
            _consecutiveCriticalFailures,
            _globalPauseReason);

    private static void ValidateAccountReference(string accountReference)
    {
        if (string.IsNullOrWhiteSpace(accountReference))
        {
            throw new ArgumentException("Account reference is required.", nameof(accountReference));
        }
    }
}
