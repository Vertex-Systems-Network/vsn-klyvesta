using Klyvesta.Application.Brokerage;

var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
var tests = new (string Id, Action Run)[]
{
    ("BTR-001 valid signed event accepted", VerifyValidSignedEvent),
    ("BTR-002 invalid signature rejected", VerifyInvalidSignature),
    ("BTR-003 stale event rejected", VerifyStaleEvent),
    ("BTR-004 future event rejected", VerifyFutureEvent),
    ("BTR-005 duplicate event id rejected", VerifyDuplicateEventId),
    ("BTR-006 duplicate nonce rejected", VerifyDuplicateNonce),
    ("BTR-007 fresh market quote accepted", VerifyFreshMarketQuote),
    ("BTR-008 closed market rejected", VerifyClosedMarket),
    ("BTR-009 stale market quote rejected", VerifyStaleMarketQuote),
    ("BTR-010 future market quote rejected", VerifyFutureMarketQuote),
    ("BTR-011 deviating market quote rejected", VerifyMarketDeviation),
    ("BTR-012 invalid market quote rejected", VerifyInvalidMarketQuote),
    ("BTR-013 account mismatch pauses only account", VerifyAccountPause),
    ("BTR-014 repeated critical dependency failures pause globally", VerifyGlobalPause),
    ("BTR-015 global pause cannot clear without healthy evidence", VerifyUnsafeGlobalClear),
    ("BTR-016 verified recovery clears global pause", VerifyVerifiedGlobalRecovery),
};

var failures = new List<string>();
foreach (var (id, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"BROKER_TRUST_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"BROKER_TRUST_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Broker trust assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("PROVIDER_NEUTRAL: signature algorithm and transport mapping remain adapter-specific and must come from direct partner evidence.");
Console.WriteLine("NOT_LIVE: no pyPSX endpoint, credential, customer PII or real-money operation is exercised.");

return failures.Count == 0 ? 0 : 1;

void VerifyValidSignedEvent()
{
    var gate = EventGate(validSignature: true);
    var decision = gate.Evaluate(Event("evt-1", "nonce-1", now), now);
    Require(decision.IsAccepted, "valid fresh signed event must be accepted");
}

void VerifyInvalidSignature()
{
    var gate = EventGate(validSignature: false);
    var decision = gate.Evaluate(Event("evt-2", "nonce-2", now), now);
    Require(!decision.IsAccepted && decision.ReasonCode == "BROKER_EVENT_SIGNATURE_INVALID", "invalid signature must fail closed");
}

void VerifyStaleEvent()
{
    var gate = EventGate(validSignature: true);
    var decision = gate.Evaluate(Event("evt-3", "nonce-3", now.AddMinutes(-6)), now);
    Require(!decision.IsAccepted && decision.ReasonCode == "BROKER_EVENT_STALE", "stale event must fail closed");
}

void VerifyFutureEvent()
{
    var gate = EventGate(validSignature: true);
    var decision = gate.Evaluate(Event("evt-4", "nonce-4", now.AddMinutes(2)), now);
    Require(!decision.IsAccepted && decision.ReasonCode == "BROKER_EVENT_FROM_FUTURE", "future event outside skew must fail closed");
}

void VerifyDuplicateEventId()
{
    var gate = EventGate(validSignature: true);
    Require(gate.Evaluate(Event("evt-5", "nonce-5", now), now).IsAccepted, "first event must pass");
    var replay = gate.Evaluate(Event("evt-5", "nonce-6", now), now);
    Require(!replay.IsAccepted && replay.ReasonCode == "BROKER_EVENT_REPLAY_EVENT_ID", "duplicate event id must be rejected");
}

void VerifyDuplicateNonce()
{
    var gate = EventGate(validSignature: true);
    Require(gate.Evaluate(Event("evt-6", "nonce-7", now), now).IsAccepted, "first nonce must pass");
    var replay = gate.Evaluate(Event("evt-7", "nonce-7", now), now);
    Require(!replay.IsAccepted && replay.ReasonCode == "BROKER_EVENT_REPLAY_NONCE", "duplicate nonce must be rejected");
}

void VerifyFreshMarketQuote()
{
    var decision = MarketGate().Evaluate(Quote(100m, now), trustedReferencePrice: 100m, marketSessionOpen: true, now);
    Require(decision.IsAccepted, "fresh in-range quote must pass");
}

void VerifyClosedMarket()
{
    var decision = MarketGate().Evaluate(Quote(100m, now), 100m, marketSessionOpen: false, now);
    Require(!decision.IsAccepted && decision.ReasonCode == "MARKET_SESSION_CLOSED", "closed market must fail closed");
}

void VerifyStaleMarketQuote()
{
    var decision = MarketGate().Evaluate(Quote(100m, now.AddSeconds(-31)), 100m, marketSessionOpen: true, now);
    Require(!decision.IsAccepted && decision.ReasonCode == "MARKET_DATA_STALE", "stale quote must be rejected");
}

void VerifyFutureMarketQuote()
{
    var decision = MarketGate().Evaluate(Quote(100m, now.AddSeconds(6)), 100m, marketSessionOpen: true, now);
    Require(!decision.IsAccepted && decision.ReasonCode == "MARKET_DATA_FROM_FUTURE", "future quote must be rejected");
}

void VerifyMarketDeviation()
{
    var decision = MarketGate().Evaluate(Quote(111m, now), 100m, marketSessionOpen: true, now);
    Require(!decision.IsAccepted && decision.ReasonCode == "MARKET_DATA_DEVIATION_EXCEEDED", "outlier quote must be rejected");
}

void VerifyInvalidMarketQuote()
{
    var decision = MarketGate().Evaluate(Quote(0m, now), 100m, marketSessionOpen: true, now);
    Require(!decision.IsAccepted && decision.ReasonCode == "MARKET_DATA_INVALID", "zero-price quote must be rejected");
}

void VerifyAccountPause()
{
    var gate = new BrokerDependencySafetyGate();
    gate.PauseAccount("acct-1");
    var blocked = gate.EvaluateExecution("acct-1");
    var unaffected = gate.EvaluateExecution("acct-2");

    Require(!blocked.IsAllowed && blocked.ReasonCode == "BROKER_DEPENDENCY_ACCOUNT_PAUSE", "mismatched account must be paused");
    Require(unaffected.IsAllowed, "unrelated account must remain allowed before global pause");

    gate.ClearAccountPauseAfterVerifiedReconciliation("acct-1");
    Require(gate.EvaluateExecution("acct-1").IsAllowed, "verified reconciliation must clear account pause");
}

void VerifyGlobalPause()
{
    var gate = new BrokerDependencySafetyGate(criticalFailureThreshold: 2);
    gate.RecordCriticalDependencyFailure("BROKER_DOWN");
    Require(gate.EvaluateExecution("acct-1").IsAllowed, "single failure below threshold must not globally pause");
    gate.RecordCriticalDependencyFailure("BROKER_DOWN");

    var blocked = gate.EvaluateExecution("acct-1");
    Require(!blocked.IsAllowed && blocked.ReasonCode == "BROKER_DOWN", "threshold failure must globally pause execution");
}

void VerifyUnsafeGlobalClear()
{
    var gate = new BrokerDependencySafetyGate(criticalFailureThreshold: 1);
    gate.RecordCriticalDependencyFailure("BROKER_DOWN");

    var threw = false;
    try
    {
        gate.ClearGlobalPauseAfterVerifiedRecovery();
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Require(threw, "global pause must not clear before healthy evidence");
}

void VerifyVerifiedGlobalRecovery()
{
    var gate = new BrokerDependencySafetyGate(criticalFailureThreshold: 1);
    gate.RecordCriticalDependencyFailure("BROKER_DOWN");
    gate.RecordHealthyDependencyEvidence();
    gate.ClearGlobalPauseAfterVerifiedRecovery();

    Require(gate.EvaluateExecution("acct-1").IsAllowed, "verified recovery must restore execution eligibility");
    Require(!gate.GetSnapshot().GlobalAutoPaused, "global pause must be cleared after verified recovery");
}

BrokerEventTrustGate EventGate(bool validSignature) =>
    new(new FixedSignatureVerifier(validSignature), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30));

BrokerEventEnvelope Event(string eventId, string nonce, DateTimeOffset sentAt) =>
    new(eventId, nonce, sentAt, "sha256:synthetic", "sig:synthetic", "key-1");

MarketDataTrustGate MarketGate() =>
    new(new MarketDataTrustPolicy(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 0.10m));

MarketDataQuote Quote(decimal price, DateTimeOffset observedAt) =>
    new("PSX:SYNTH", "synthetic-market-data", price, observedAt);

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class FixedSignatureVerifier(bool result) : IBrokerEventSignatureVerifier
{
    public bool Verify(BrokerEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return result;
    }
}
