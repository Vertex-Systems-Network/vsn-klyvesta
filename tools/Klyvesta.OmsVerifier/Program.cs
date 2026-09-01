using Klyvesta.Application.Brokerage;
using Klyvesta.Application.Orders;
using Klyvesta.Domain.Orders;
using Klyvesta.Infrastructure.Brokerage.Paper;

var tests = new (string Id, Func<Task> Run)[]
{
    ("OMS-001 rejected validation never reaches broker", VerifyRejectedValidationAsync),
    ("OMS-002 full fill consumes and releases reservation exactly", VerifyFullFillReservationAsync),
    ("OMS-003 repeated execute cannot create a second broker order", VerifyRepeatedExecuteAsync),
    ("OMS-004 ambiguous submit becomes UNKNOWN and recovers by query", VerifyUnknownRecoveryAsync),
    ("OMS-005 partial fill consumes only confirmed economics", VerifyPartialFillAsync),
    ("OMS-006 duplicate execution snapshot is idempotent", VerifyDuplicateExecutionSnapshotAsync),
    ("OMS-007 partial-fill cancel race preserves fills and releases remainder", VerifyPartialFillCancelAsync),
    ("OMS-008 out-of-order status cannot regress authoritative state", VerifyOutOfOrderStatusAsync),
    ("OMS-009 pre-side-effect retry keeps one internal broker order", VerifySafeRetryAsync),
    ("OMS-010 contradictory snapshot quarantines to reconciliation", VerifyContradictorySnapshotAsync),
};

var failures = new List<string>();
foreach (var (id, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"OMS_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"OMS_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"OMS assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("STACKED_ON_P1_06: this verifier requires the non-live PaperBrokerAdapter slice from draft PR #68.");
Console.WriteLine("NOT_ACCEPTED_AS_FULL_P1: ledger, portfolio reconciliation, authorization, market-data freshness, mandate, risk, compliance and AI shadow evidence remain separate P1 work.");
Console.WriteLine("NOT_LIVE: no pyPSX mapping, broker credentials, production PII or real-money authority is exercised.");

if (failures.Count > 0)
{
    Console.Error.WriteLine("OMS verification failed.");
    return 1;
}

return 0;

static async Task VerifyRejectedValidationAsync()
{
    var adapter = new PaperBrokerAdapter();
    using var service = CreateService(adapter);
    var request = CreateRequest(1, 10m);

    await service.CreateIntentAsync(request);
    await service.BeginValidationAsync(request.OrderIntentId);
    var rejected = await service.RecordValidationAsync(
        request.OrderIntentId,
        new OrderValidationEvidence(false, "POLICY_DENY", "synthetic-policy-v1", "synthetic-evidence-1"),
        reservationSpec: null);
    var execute = await service.ExecuteAsync(request.OrderIntentId);

    Require(rejected.IntentState == OrderIntentState.Rejected, "rejected validation must be terminal");
    Require(execute.BrokerOrderId is null, "rejected validation must not create a broker order");
    Require(adapter.AcceptedOrderCount == 0, "rejected validation must never invoke an accepted broker order");
}

static async Task VerifyFullFillReservationAsync()
{
    var adapter = new PaperBrokerAdapter();
    using var service = CreateService(adapter);
    var request = CreateRequest(2, 10m);
    await PrepareApprovedAsync(service, request, reservationAmount: 1_500m);

    var result = await service.ExecuteAsync(request.OrderIntentId);

    Require(result.BrokerOrderState == ManagedBrokerOrderState.Filled, "full fill must finish FILLED");
    Require(result.FilledQuantity == 10m, "full fill quantity must be exact");
    Require(result.ConsumedReservationAmount == 1_000m, "cash reservation must consume exact fill notional");
    Require(result.ReleasedReservationAmount == 500m, "unused cash reservation must release after conclusive fill");
    Require(result.RemainingReservationAmount == 0m, "terminal fill must leave no unresolved reservation");
    Require(result.ReservationState == ReservationState.Released, "over-reserved full fill must finish RELEASED after unused remainder release");
}

static async Task VerifyRepeatedExecuteAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.Open,
    });
    using var service = CreateService(adapter);
    var request = CreateRequest(3, 6m);
    await PrepareApprovedAsync(service, request, reservationAmount: 600m);

    var first = await service.ExecuteAsync(request.OrderIntentId);
    var second = await service.ExecuteAsync(request.OrderIntentId);

    Require(first.BrokerOrderState == ManagedBrokerOrderState.Open, "first execution must create an open paper order");
    Require(second.BrokerOrderId == first.BrokerOrderId, "repeated execute must reuse the internal broker order");
    Require(adapter.AcceptedOrderCount == 1, "repeated execute must not create another broker order");
}

static async Task VerifyUnknownRecoveryAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.FullFill,
        AmbiguousTimeoutAfterAcceptance = true,
    });
    using var service = CreateService(adapter);
    var request = CreateRequest(4, 5m);
    await PrepareApprovedAsync(service, request, reservationAmount: 500m);

    var unknown = await service.ExecuteAsync(request.OrderIntentId);
    var replay = await service.ExecuteAsync(request.OrderIntentId);
    var openBreaks = await service.GetOpenReconciliationAsync();

    Require(unknown.BrokerOrderState == ManagedBrokerOrderState.Unknown, "ambiguous submit must become UNKNOWN");
    Require(unknown.ReconciliationRequired, "UNKNOWN must queue reconciliation");
    Require(unknown.ConsumedReservationAmount == 0m, "ambiguous response must not fabricate local fill economics");
    Require(unknown.RemainingReservationAmount == 500m, "UNKNOWN must retain unresolved reservation");
    Require(replay.BrokerOrderState == ManagedBrokerOrderState.Unknown, "repeat execute must not leave UNKNOWN");
    Require(adapter.AcceptedOrderCount == 1, "UNKNOWN must not be blindly resubmitted");
    Require(openBreaks.Count == 1, "UNKNOWN must create one open reconciliation item");

    var recovered = await service.RecoverUnknownAsync(request.OrderIntentId);
    var afterRecoveryBreaks = await service.GetOpenReconciliationAsync();

    Require(recovered.BrokerOrderState == ManagedBrokerOrderState.Filled, "broker query evidence must recover UNKNOWN to FILLED");
    Require(recovered.ConsumedReservationAmount == 500m, "recovery must consume exact confirmed fill economics once");
    Require(recovered.RemainingReservationAmount == 0m, "recovered fill must resolve reservation");
    Require(!recovered.ReconciliationRequired, "resolved UNKNOWN must close its reconciliation item");
    Require(afterRecoveryBreaks.Count == 0, "no reconciliation item may remain open after conclusive recovery");
}

static async Task VerifyPartialFillAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.PartialFill,
        PartialFillRatio = 0.4m,
    });
    using var service = CreateService(adapter);
    var request = CreateRequest(5, 10m);
    await PrepareApprovedAsync(service, request, reservationAmount: 1_000m);

    var result = await service.ExecuteAsync(request.OrderIntentId);

    Require(result.BrokerOrderState == ManagedBrokerOrderState.PartiallyFilled, "partial fill must remain PARTIALLY_FILLED");
    Require(result.FilledQuantity == 4m, "partial fill quantity must follow confirmed execution evidence");
    Require(result.ConsumedReservationAmount == 400m, "partial fill must consume only confirmed fill notional");
    Require(result.RemainingReservationAmount == 600m, "unfilled quantity economics must remain reserved");
    Require(result.ReservationState == ReservationState.PartiallyConsumed, "reservation state must reflect partial consumption");
}

static async Task VerifyDuplicateExecutionSnapshotAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.PartialFill,
        PartialFillRatio = 0.4m,
    });
    using var service = CreateService(adapter);
    var request = CreateRequest(6, 10m);
    await PrepareApprovedAsync(service, request, reservationAmount: 1_000m);
    _ = await service.ExecuteAsync(request.OrderIntentId);

    var brokerSnapshot = RequireValue(await adapter.GetOrderAsync(request.BrokerOrderId), BrokerResultState.Success);
    var firstReplay = await service.ApplyBrokerSnapshotAsync(request.OrderIntentId, brokerSnapshot);
    var secondReplay = await service.ApplyBrokerSnapshotAsync(request.OrderIntentId, brokerSnapshot);

    Require(firstReplay.ConsumedReservationAmount == 400m, "first duplicate snapshot replay must not double-consume reservation");
    Require(secondReplay.ConsumedReservationAmount == 400m, "repeated duplicate snapshot must remain idempotent");
    Require(secondReplay.FilledQuantity == 4m, "duplicate execution evidence must remain one fill effect");
}

static async Task VerifyPartialFillCancelAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.PartialFill,
        PartialFillRatio = 0.25m,
    });
    using var service = CreateService(adapter);
    var request = CreateRequest(7, 8m);
    await PrepareApprovedAsync(service, request, reservationAmount: 800m);

    var partial = await service.ExecuteAsync(request.OrderIntentId);
    var cancelled = await service.CancelAsync(request.OrderIntentId);

    Require(partial.FilledQuantity == 2m, "race setup must contain a confirmed partial fill");
    Require(cancelled.BrokerOrderState == ManagedBrokerOrderState.Cancelled, "conclusive cancel must finish CANCELLED");
    Require(cancelled.FilledQuantity == 2m, "cancel must never erase confirmed fills");
    Require(cancelled.ConsumedReservationAmount == 200m, "only confirmed fill economics may be consumed");
    Require(cancelled.ReleasedReservationAmount == 600m, "cancel must release only unused reservation");
    Require(cancelled.RemainingReservationAmount == 0m, "cancelled order must not retain unused reservation");
}

static async Task VerifyOutOfOrderStatusAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.Open,
    });
    using var service = CreateService(adapter);
    var request = CreateRequest(8, 5m);
    await PrepareApprovedAsync(service, request, reservationAmount: 500m);
    _ = await service.ExecuteAsync(request.OrderIntentId);

    BrokerExecution execution = new(
        "OMS-008-execution",
        request.BrokerOrderId,
        request.InstrumentReference,
        2m,
        100m,
        DateTimeOffset.UnixEpoch.AddMinutes(8));
    var eventResult = adapter.ApplyPaperEvent(new PaperBrokerEvent(
        "OMS-008-event",
        request.BrokerOrderId,
        BrokerOrderState.PartiallyFilled,
        execution));

    var partial = await service.ApplyBrokerSnapshotAsync(request.OrderIntentId, eventResult.Snapshot);
    BrokerOrderSnapshot olderStatus = eventResult.Snapshot with { State = BrokerOrderState.Open };
    var afterOlderStatus = await service.ApplyBrokerSnapshotAsync(request.OrderIntentId, olderStatus);

    Require(partial.BrokerOrderState == ManagedBrokerOrderState.PartiallyFilled, "confirmed fill must advance state to PARTIALLY_FILLED");
    Require(afterOlderStatus.BrokerOrderState == ManagedBrokerOrderState.PartiallyFilled, "older OPEN status must not regress authoritative state");
    Require(afterOlderStatus.ConsumedReservationAmount == 200m, "out-of-order status must not duplicate execution economics");
}

static async Task VerifySafeRetryAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        TimeoutBeforeSideEffect = true,
    });
    using var service = CreateService(adapter);
    var request = CreateRequest(9, 3m);
    await PrepareApprovedAsync(service, request, reservationAmount: 300m);

    var first = await service.ExecuteAsync(request.OrderIntentId);
    var second = await service.ExecuteAsync(request.OrderIntentId);

    Require(first.BrokerOrderId == request.BrokerOrderId, "safe retry path must persist one internal broker order identity");
    Require(first.BrokerOrderState == ManagedBrokerOrderState.PendingSubmit, "known pre-side-effect failure may return to PENDING_SUBMIT");
    Require(second.BrokerOrderId == request.BrokerOrderId, "retry must reuse the same broker order identity");
    Require(adapter.AcceptedOrderCount == 0, "known pre-side-effect failure must create no external paper order");
    Require(second.RemainingReservationAmount == 300m, "safe retry failure must retain reservation until outcome is conclusive");
}

static async Task VerifyContradictorySnapshotAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.Open,
    });
    using var service = CreateService(adapter);
    var request = CreateRequest(10, 4m);
    await PrepareApprovedAsync(service, request, reservationAmount: 400m);
    _ = await service.ExecuteAsync(request.OrderIntentId);

    BrokerOrderSnapshot contradiction = new(
        request.BrokerOrderId,
        $"paper-order-{request.BrokerOrderId:N}",
        BrokerOrderState.PartiallyFilled,
        4m,
        3m,
        [new BrokerExecution("OMS-010-execution", request.BrokerOrderId, request.InstrumentReference, 2m, 100m, DateTimeOffset.UnixEpoch)],
        "SYNTHETIC_CONTRADICTION");

    var quarantined = await service.ApplyBrokerSnapshotAsync(request.OrderIntentId, contradiction);
    var breaks = await service.GetOpenReconciliationAsync();

    Require(quarantined.BrokerOrderState == ManagedBrokerOrderState.Unknown, "contradictory broker quantities must quarantine order to UNKNOWN");
    Require(quarantined.ReconciliationRequired, "contradictory snapshot must create reconciliation work");
    Require(quarantined.ConsumedReservationAmount == 0m, "contradictory snapshot must not consume untrusted execution economics");
    Require(quarantined.RemainingReservationAmount == 400m, "contradictory snapshot must retain unresolved reservation");
    Require(breaks.Count == 1, "contradictory snapshot must create exactly one open reconciliation item");
}

static OrderManagementService CreateService(PaperBrokerAdapter adapter) =>
    new(adapter, () => DateTimeOffset.UnixEpoch.AddHours(1));

static async Task PrepareApprovedAsync(OrderManagementService service, OmsOrderRequest request, decimal reservationAmount)
{
    await service.CreateIntentAsync(request);
    await service.BeginValidationAsync(request.OrderIntentId);
    await service.RecordValidationAsync(
        request.OrderIntentId,
        new OrderValidationEvidence(true, "ALLOW_SYNTHETIC_POLICY", "synthetic-policy-v1", $"synthetic-evidence-{request.OrderIntentId:N}"),
        new OmsReservationSpec(CreateGuid(2_000 + GetSeed(request.OrderIntentId)), ReservationKind.Cash, reservationAmount));
    await service.QueueForExecutionAsync(request.OrderIntentId);
}

static OmsOrderRequest CreateRequest(int seed, decimal quantity) => new(
    CreateGuid(seed),
    CreateGuid(1_000 + seed),
    $"OMS-{seed:D3}-key",
    $"paper-account-{seed:D3}",
    "SYNTH",
    BrokerOrderSide.Buy,
    BrokerOrderType.Limit,
    quantity,
    100m,
    BrokerTimeInForce.Day);

static Guid CreateGuid(int seed) => Guid.Parse($"00000000-0000-0000-0000-{seed:D12}");

static int GetSeed(Guid id) => int.Parse(id.ToString("N")[20..], System.Globalization.CultureInfo.InvariantCulture);

static T RequireValue<T>(BrokerOperationResult<T> result, BrokerResultState expectedState)
{
    Require(result.State == expectedState, $"expected broker result {expectedState} but got {result.State}");
    return result.Value ?? throw new InvalidOperationException("Expected broker result value was missing.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
