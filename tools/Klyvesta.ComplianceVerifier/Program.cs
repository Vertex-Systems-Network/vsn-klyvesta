using Klyvesta.Application.Brokerage;
using Klyvesta.Application.Compliance;
using Klyvesta.Domain.Compliance;
using Klyvesta.Infrastructure.Brokerage.Paper;

var tests = new (string Id, Func<Task> Run)[]
{
    ("COM-001 eligible manual paper order is allowed without mandate", VerifyManualAllowAsync),
    ("COM-002 restricted account is denied", () => VerifyDeniedAsync(Request(accountStatus: ComplianceAccountStatus.Restricted), "COMPLIANCE_ACCOUNT_RESTRICTED")),
    ("COM-003 suspended account is denied", () => VerifyDeniedAsync(Request(accountStatus: ComplianceAccountStatus.Suspended), "COMPLIANCE_ACCOUNT_SUSPENDED")),
    ("COM-004 pending account review holds", () => VerifyHeldAsync(Request(accountStatus: ComplianceAccountStatus.PendingReview), "COMPLIANCE_ACCOUNT_PENDING_REVIEW")),
    ("COM-005 unknown account status holds", () => VerifyHeldAsync(Request(accountStatus: ComplianceAccountStatus.Unknown), "COMPLIANCE_ACCOUNT_STATUS_UNKNOWN")),
    ("COM-006 disabled regulatory feature is denied", () => VerifyDeniedAsync(Request(featureStatus: RegulatoryFeatureStatus.Disabled), "COMPLIANCE_REGULATORY_FEATURE_DISABLED")),
    ("COM-007 pending regulatory feature holds", () => VerifyHeldAsync(Request(featureStatus: RegulatoryFeatureStatus.Pending), "COMPLIANCE_REGULATORY_FEATURE_PENDING")),
    ("COM-008 unknown regulatory feature holds", () => VerifyHeldAsync(Request(featureStatus: RegulatoryFeatureStatus.Unknown), "COMPLIANCE_REGULATORY_FEATURE_UNKNOWN")),
    ("COM-009 pending manual review holds", () => VerifyHeldAsync(Request(reviewStatus: ManualReviewStatus.Pending), "COMPLIANCE_MANUAL_REVIEW_PENDING")),
    ("COM-010 rejected manual review is denied", () => VerifyDeniedAsync(Request(reviewStatus: ManualReviewStatus.Rejected), "COMPLIANCE_MANUAL_REVIEW_REJECTED")),
    ("COM-011 restricted instrument is denied", () => VerifyDeniedAsync(Request(instrumentRestriction: InstrumentRestrictionStatus.Restricted), "COMPLIANCE_INSTRUMENT_RESTRICTED")),
    ("COM-012 instrument manual review holds", () => VerifyHeldAsync(Request(instrumentRestriction: InstrumentRestrictionStatus.ManualReview), "COMPLIANCE_INSTRUMENT_MANUAL_REVIEW")),
    ("COM-013 unknown instrument restriction holds", () => VerifyHeldAsync(Request(instrumentRestriction: InstrumentRestrictionStatus.Unknown), "COMPLIANCE_INSTRUMENT_RESTRICTION_UNKNOWN")),
    ("COM-014 auto simulation without mandate holds", () => VerifyHeldAsync(Request(mode: PaperExecutionMode.AutoSimulation, mandate: null, useDefaultMandate: false), "COMPLIANCE_MANDATE_MISSING")),
    ("COM-015 pending mandate holds", () => VerifyHeldAsync(Request(mode: PaperExecutionMode.AutoSimulation, mandate: Mandate(MandateStatus.Pending)), "COMPLIANCE_MANDATE_PENDING")),
    ("COM-016 unknown mandate status holds", () => VerifyHeldAsync(Request(mode: PaperExecutionMode.AutoSimulation, mandate: Mandate(MandateStatus.Unknown)), "COMPLIANCE_MANDATE_STATUS_UNKNOWN")),
    ("COM-017 expired mandate is denied", () => VerifyDeniedAsync(Request(mode: PaperExecutionMode.AutoSimulation, mandate: Mandate(MandateStatus.Expired)), "COMPLIANCE_MANDATE_EXPIRED")),
    ("COM-018 revoked mandate is denied", () => VerifyDeniedAsync(Request(mode: PaperExecutionMode.AutoSimulation, mandate: Mandate(MandateStatus.Revoked)), "COMPLIANCE_MANDATE_REVOKED")),
    ("COM-019 active but not-yet-effective mandate holds", () => VerifyHeldAsync(Request(mode: PaperExecutionMode.AutoSimulation, mandate: ActiveMandate(Now().AddMinutes(5), Now().AddHours(1))), "COMPLIANCE_MANDATE_NOT_YET_EFFECTIVE")),
    ("COM-020 active mandate past its effective window is denied", () => VerifyDeniedAsync(Request(mode: PaperExecutionMode.AutoSimulation, mandate: ActiveMandate(Now().AddHours(-2), Now().AddMinutes(-1))), "COMPLIANCE_MANDATE_EXPIRED")),
    ("COM-021 active in-window mandate allows auto simulation", VerifyAutoAllowAsync),
    ("COM-022 definitive denial outranks simultaneous hold", VerifyDenyPrecedenceAsync),
    ("COM-023 compliance deny blocks inner PaperBroker and records decision", VerifyAdapterDenyAsync),
    ("COM-024 compliance hold blocks inner PaperBroker and records decision", VerifyAdapterHoldAsync),
    ("COM-025 compliance allow forwards exactly once", VerifyAdapterAllowAsync),
};

var failures = new List<string>();
foreach (var (id, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"COMPLIANCE_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"COMPLIANCE_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Compliance assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("STACKED_ON_P1_09: this verifier requires the non-live PaperBroker, OMS, portfolio/reconciliation and deterministic risk slices from draft PRs #68, #71, #73 and #75.");
Console.WriteLine("FAIL_CLOSED: restricted/prohibited states deny; pending/unknown/manual-review states hold; only complete compliant evidence can allow.");
Console.WriteLine("MANDATE_BOUNDARY: Auto simulation requires mandate evidence when policy requires it; manual paper mode does not fabricate that requirement.");
Console.WriteLine("NO_AI_AUTHORITY: no AI/caller override exists in the compliance decision contract.");
Console.WriteLine("NOT_ACCEPTED_AS_FULL_P1: authorization/BOLA, ledger, AI shadow and remaining exit-gate evidence remain separate work.");
Console.WriteLine("NOT_LIVE: no pyPSX mapping, live credentials, production customer data, production KYC/AML provider, personalized production advice or real-money authority is exercised.");

return failures.Count == 0 ? 0 : 1;

static Task VerifyManualAllowAsync()
{
    var decision = Evaluate(Request(mode: PaperExecutionMode.Manual, mandate: null, useDefaultMandate: false));
    Require(decision.Outcome == ComplianceDecisionOutcome.Allow, "eligible manual paper order must allow without mandate");
    Require(decision.PrimaryReasonCode == "COMPLIANCE_ALLOW", "allow reason must be explicit");
    Require(decision.PolicyVersion == "paper-compliance-v1", "allow must retain policy version");
    return Task.CompletedTask;
}

static Task VerifyAutoAllowAsync()
{
    var decision = Evaluate(Request(mode: PaperExecutionMode.AutoSimulation, mandate: ActiveMandate(Now().AddHours(-1), Now().AddHours(1))));
    Require(decision.Outcome == ComplianceDecisionOutcome.Allow, "active in-window mandate must allow otherwise-compliant Auto simulation");
    Require(decision.ExecutionMode == PaperExecutionMode.AutoSimulation, "decision must retain execution mode evidence");
    Require(decision.PolicyVersion == "paper-compliance-v1", "Auto allow must retain policy version");
    return Task.CompletedTask;
}

static Task VerifyDenyPrecedenceAsync()
{
    var decision = Evaluate(Request(
        accountStatus: ComplianceAccountStatus.Restricted,
        featureStatus: RegulatoryFeatureStatus.Pending));
    Require(decision.Outcome == ComplianceDecisionOutcome.Deny, "definitive denial must outrank simultaneous unresolved hold");
    Require(decision.PrimaryReasonCode == "COMPLIANCE_ACCOUNT_RESTRICTED", "primary reason must be first definitive denial");
    Require(decision.ReasonCodes.Contains("COMPLIANCE_REGULATORY_FEATURE_PENDING", StringComparer.Ordinal), "secondary hold reason must remain observable");
    return Task.CompletedTask;
}

static Task VerifyDeniedAsync(ComplianceEvaluationRequest request, string reasonCode)
{
    var decision = Evaluate(request);
    Require(decision.Outcome == ComplianceDecisionOutcome.Deny, $"expected DENY for {reasonCode}, got {decision.Outcome}");
    Require(decision.ReasonCodes.Contains(reasonCode, StringComparer.Ordinal), $"expected denial reason {reasonCode}");
    Require(decision.PolicyVersion == "paper-compliance-v1", "denial must retain policy version");
    return Task.CompletedTask;
}

static Task VerifyHeldAsync(ComplianceEvaluationRequest request, string reasonCode)
{
    var decision = Evaluate(request);
    Require(decision.Outcome == ComplianceDecisionOutcome.Hold, $"expected HOLD for {reasonCode}, got {decision.Outcome}");
    Require(decision.ReasonCodes.Contains(reasonCode, StringComparer.Ordinal), $"expected hold reason {reasonCode}");
    Require(decision.PolicyVersion == "paper-compliance-v1", "hold must retain policy version");
    return Task.CompletedTask;
}

static async Task VerifyAdapterDenyAsync()
{
    var inner = CreatePaperBroker();
    var contextProvider = new FixedComplianceContextProvider(Context(accountStatus: ComplianceAccountStatus.Restricted));
    var restrictionProvider = new FixedInstrumentRestrictionProvider(InstrumentRestrictionStatus.Allowed);
    var guarded = new ComplianceGuardBrokerAdapter(inner, new DeterministicComplianceGate(), contextProvider, restrictionProvider, FixedClock);
    var command = Command(23);

    var result = await guarded.SubmitOrderAsync(command);
    var decision = guarded.GetRecordedDecision(command.BrokerOrderId);

    Require(result.State == BrokerResultState.Rejected, "compliance denial must return rejected pre-side-effect result");
    Require(inner.AcceptedOrderCount == 0, "compliance denial must not reach inner PaperBroker");
    Require(decision is not null && decision.Outcome == ComplianceDecisionOutcome.Deny, "denial must be recorded against broker-order id");
    Require(decision!.PrimaryReasonCode == "COMPLIANCE_ACCOUNT_RESTRICTED", "recorded denial must retain exact reason");
}

static async Task VerifyAdapterHoldAsync()
{
    var inner = CreatePaperBroker();
    var contextProvider = new FixedComplianceContextProvider(Context(reviewStatus: ManualReviewStatus.Pending));
    var restrictionProvider = new FixedInstrumentRestrictionProvider(InstrumentRestrictionStatus.Allowed);
    var guarded = new ComplianceGuardBrokerAdapter(inner, new DeterministicComplianceGate(), contextProvider, restrictionProvider, FixedClock);
    var command = Command(24);

    var result = await guarded.SubmitOrderAsync(command);
    var decision = guarded.GetRecordedDecision(command.BrokerOrderId);

    Require(result.State == BrokerResultState.RetryableFailure, "compliance hold must return retryable pre-side-effect result");
    Require(inner.AcceptedOrderCount == 0, "compliance hold must not reach inner PaperBroker");
    Require(decision is not null && decision.Outcome == ComplianceDecisionOutcome.Hold, "hold must be recorded against broker-order id");
    Require(decision!.PrimaryReasonCode == "COMPLIANCE_MANUAL_REVIEW_PENDING", "recorded hold must retain exact reason");
}

static async Task VerifyAdapterAllowAsync()
{
    var inner = CreatePaperBroker();
    var contextProvider = new FixedComplianceContextProvider(Context());
    var restrictionProvider = new FixedInstrumentRestrictionProvider(InstrumentRestrictionStatus.Allowed);
    var guarded = new ComplianceGuardBrokerAdapter(inner, new DeterministicComplianceGate(), contextProvider, restrictionProvider, FixedClock);
    var command = Command(25);

    var result = await guarded.SubmitOrderAsync(command);
    var decision = guarded.GetRecordedDecision(command.BrokerOrderId);

    Require(result.State == BrokerResultState.Success, "compliance allow must forward to inner PaperBroker");
    Require(inner.AcceptedOrderCount == 1, "compliance allow must reach inner PaperBroker exactly once");
    Require(decision is not null && decision.Outcome == ComplianceDecisionOutcome.Allow, "allow must be recorded against broker-order id");
    Require(decision!.PolicyVersion == "paper-compliance-v1", "recorded allow must retain policy version");
}

static ComplianceDecision Evaluate(ComplianceEvaluationRequest request, PaperCompliancePolicy? policy = null) =>
    new DeterministicComplianceGate().Evaluate(policy ?? Policy(), request);

static PaperCompliancePolicy Policy(
    bool paperOrderSubmissionEnabled = true,
    bool requireMandateForAutoSimulation = true) =>
    new("paper-compliance-v1", paperOrderSubmissionEnabled, requireMandateForAutoSimulation);

static ComplianceEvaluationRequest Request(
    PaperExecutionMode mode = PaperExecutionMode.Manual,
    ComplianceAccountStatus accountStatus = ComplianceAccountStatus.Eligible,
    RegulatoryFeatureStatus featureStatus = RegulatoryFeatureStatus.Enabled,
    ManualReviewStatus reviewStatus = ManualReviewStatus.NotRequired,
    InstrumentRestrictionStatus instrumentRestriction = InstrumentRestrictionStatus.Allowed,
    ComplianceMandateEvidence? mandate = null,
    bool useDefaultMandate = true) =>
    new(
        "paper-compliance-account",
        "AAA",
        mode,
        accountStatus,
        featureStatus,
        reviewStatus,
        instrumentRestriction,
        useDefaultMandate && mode == PaperExecutionMode.AutoSimulation
            ? mandate ?? ActiveMandate(Now().AddHours(-1), Now().AddHours(1))
            : mandate,
        Now());

static ComplianceSubmissionContext Context(
    PaperExecutionMode mode = PaperExecutionMode.Manual,
    ComplianceAccountStatus accountStatus = ComplianceAccountStatus.Eligible,
    RegulatoryFeatureStatus featureStatus = RegulatoryFeatureStatus.Enabled,
    ManualReviewStatus reviewStatus = ManualReviewStatus.NotRequired,
    ComplianceMandateEvidence? mandate = null) =>
    new(
        Policy(),
        mode,
        accountStatus,
        featureStatus,
        reviewStatus,
        mandate);

static ComplianceMandateEvidence Mandate(MandateStatus status) =>
    new(
        "paper-mandate-001",
        status,
        Now().AddHours(-1),
        Now().AddHours(1));

static ComplianceMandateEvidence ActiveMandate(DateTimeOffset effectiveFrom, DateTimeOffset effectiveUntil) =>
    new("paper-mandate-active", MandateStatus.Active, effectiveFrom, effectiveUntil);

static PaperBrokerAdapter CreatePaperBroker() =>
    new(new PaperBrokerProfile
    {
        StartingCash = 100_000m,
        SubmissionOutcome = PaperSubmissionOutcome.FullFill,
        FillPrice = 100m,
        StartTime = DateTimeOffset.UnixEpoch,
    });

static SubmitBrokerOrderCommand Command(int seed) =>
    new(
        CreateGuid(30_000 + seed),
        CreateGuid(40_000 + seed),
        $"compliance-submit-{seed:D3}",
        "paper-compliance-account",
        "AAA",
        BrokerOrderSide.Buy,
        BrokerOrderType.Limit,
        100m,
        100m,
        BrokerTimeInForce.Day);

static DateTimeOffset FixedClock() => Now();

static DateTimeOffset Now() => DateTimeOffset.UnixEpoch.AddHours(4);

static Guid CreateGuid(int seed) => Guid.Parse($"00000000-0000-0000-0000-{seed:D12}");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class FixedComplianceContextProvider(ComplianceSubmissionContext context) : IComplianceSubmissionContextProvider
{
    public ComplianceSubmissionContext GetContext(SubmitBrokerOrderCommand command, DateTimeOffset evaluatedAt)
    {
        _ = command;
        _ = evaluatedAt;
        return context;
    }
}

sealed class FixedInstrumentRestrictionProvider(InstrumentRestrictionStatus status) : IInstrumentRestrictionProvider
{
    public InstrumentRestrictionStatus GetRestriction(SubmitBrokerOrderCommand command, DateTimeOffset evaluatedAt)
    {
        _ = command;
        _ = evaluatedAt;
        return status;
    }
}
