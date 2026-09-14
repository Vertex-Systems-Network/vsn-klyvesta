using Klyvesta.Application.Brokerage;
using Klyvesta.Infrastructure.Brokerage.Paper;

var tests = new (string Id, Func<Task> Run)[]
{
    ("PB-001 full fill adapter slice", VerifyFullFillAsync),
    ("PB-002 rejected order adapter slice", VerifyRejectedOrderAsync),
    ("PB-003 partial fill adapter slice", VerifyPartialFillAsync),
    ("PB-004 multiple fills and execution dedupe adapter slice", VerifyMultipleFillsAsync),
    ("PB-005 duplicate command adapter slice", VerifyIdempotencyAsync),
    ("PB-006 pre-side-effect timeout adapter slice", VerifySafePreSideEffectFailureAsync),
    ("PB-007 ambiguous timeout adapter slice", VerifyAmbiguousTimeoutAsync),
    ("PB-008 cancel before fill adapter slice", VerifyCancelBeforeFillAsync),
    ("PB-009 fill/cancel race adapter slice", VerifyFillCancelRaceAsync),
    ("PB-010 duplicate event adapter slice", VerifyDuplicateEventAsync),
    ("PB-011 out-of-order event adapter slice", VerifyOutOfOrderEventAsync),
    ("PB-013 market closed adapter slice", VerifyMarketClosedAsync),
    ("PB-014 broker unavailable adapter slice", VerifyUnavailableAsync),
};

var failures = new List<string>();

foreach (var (id, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"ADAPTER_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"ADAPTER_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"PaperBroker adapter assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("NOT_ACCEPTED_AS_FULL_P1: PB-012 and PB-015..PB-020 require wider market-data, reconciliation, ledger, authorization, mandate, risk and compliance integration evidence.");
Console.WriteLine("NOT_LIVE: no pyPSX mapping, broker credentials, production PII or real-money authority is exercised by this verifier.");

if (failures.Count > 0)
{
    Console.Error.WriteLine("PaperBroker adapter verification failed.");
    return 1;
}

return 0;

static async Task VerifyFullFillAsync()
{
    var adapter = new PaperBrokerAdapter();
    var command = CreateCommand(1, quantity: 10m);

    var result = await adapter.SubmitOrderAsync(command);
    var order = RequireValue(result, BrokerResultState.Success);

    Require(order.State == BrokerOrderState.Filled, "full fill must finish FILLED");
    Require(order.FilledQuantity == 10m, "full fill quantity must equal requested quantity");
    Require(order.Executions.Sum(execution => execution.Quantity) == 10m, "execution sum must equal requested quantity");

    var positions = RequireValue(await adapter.GetPositionsAsync(command.AccountReference), BrokerResultState.Success);
    Require(positions.Single().Quantity == 10m, "paper position must reflect exactly one financial effect");

    var balances = RequireValue(await adapter.GetBalancesAsync(command.AccountReference), BrokerResultState.Success);
    Require(balances.Single().Cash == 999_000m, "paper cash must reflect exact-decimal fill notional once");
}

static async Task VerifyRejectedOrderAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.Rejected,
    });
    var command = CreateCommand(2, quantity: 5m);

    var result = await adapter.SubmitOrderAsync(command);
    var order = RequireValue(result, BrokerResultState.Rejected);

    Require(order.State == BrokerOrderState.Rejected, "rejected order must remain REJECTED");
    Require(order.Executions.Count == 0, "rejected order must have zero executions");

    var positions = RequireValue(await adapter.GetPositionsAsync(command.AccountReference), BrokerResultState.Success);
    Require(positions.Count == 0, "rejected order must not change executed position");
}

static async Task VerifyPartialFillAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.PartialFill,
        PartialFillRatio = 0.4m,
    });
    var command = CreateCommand(3, quantity: 10m);

    var result = await adapter.SubmitOrderAsync(command);
    var order = RequireValue(result, BrokerResultState.Success);

    Require(order.State == BrokerOrderState.PartiallyFilled, "partial fill must remain PARTIALLY_FILLED");
    Require(order.FilledQuantity == 4m, "partial fill quantity must equal configured exact ratio");
    Require(order.RequestedQuantity - order.FilledQuantity == 6m, "remaining quantity must stay exact");
}

static async Task VerifyMultipleFillsAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.FullFill,
        FillCount = 3,
    });
    var command = CreateCommand(4, quantity: 9m);

    var result = await adapter.SubmitOrderAsync(command);
    var order = RequireValue(result, BrokerResultState.Success);

    Require(order.Executions.Count == 3, "configured multiple fill count must be preserved");
    Require(order.Executions.Sum(execution => execution.Quantity) == 9m, "multiple fill aggregate must be exact");

    var duplicateExecution = order.Executions[0];
    var duplicateResult = adapter.ApplyPaperEvent(new PaperBrokerEvent(
        "PB-004-duplicate-execution-delivery",
        command.BrokerOrderId,
        BrokerOrderState.Filled,
        duplicateExecution));

    Require(duplicateResult.DuplicateExecution, "duplicate execution id must be detected");

    var positions = RequireValue(await adapter.GetPositionsAsync(command.AccountReference), BrokerResultState.Success);
    Require(positions.Single().Quantity == 9m, "duplicate execution must not create a second financial effect");
}

static async Task VerifyIdempotencyAsync()
{
    var adapter = new PaperBrokerAdapter();
    var command = CreateCommand(5, quantity: 7m, idempotencyKey: "PB-005-key");

    var first = await adapter.SubmitOrderAsync(command);
    var replay = await adapter.SubmitOrderAsync(command);
    var conflict = await adapter.SubmitOrderAsync(command with { Quantity = 8m });

    Require(first.State == BrokerResultState.Success, "first idempotent submission must succeed");
    Require(replay == first, "same key and same payload must return the existing result");
    Require(conflict.State == BrokerResultState.Rejected, "same key with different payload must reject");
    Require(conflict.ReasonCode == "IDEMPOTENCY_CONFLICT", "idempotency conflict reason must be explicit");
    Require(adapter.AcceptedOrderCount == 1, "idempotency replay/conflict must not create another order");
}

static async Task VerifySafePreSideEffectFailureAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        TimeoutBeforeSideEffect = true,
    });
    var command = CreateCommand(6, quantity: 3m);

    var result = await adapter.SubmitOrderAsync(command);

    Require(result.State == BrokerResultState.RetryableFailure, "known pre-side-effect timeout must be retryable");
    Require(result.ReasonCode == "TIMEOUT_BEFORE_SIDE_EFFECT", "pre-side-effect timeout classification must be explicit");
    Require(adapter.AcceptedOrderCount == 0, "pre-side-effect timeout must not create a broker order");
}

static async Task VerifyAmbiguousTimeoutAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        AmbiguousTimeoutAfterAcceptance = true,
        SubmissionOutcome = PaperSubmissionOutcome.FullFill,
    });
    var command = CreateCommand(7, quantity: 6m, idempotencyKey: "PB-007-key");

    var first = await adapter.SubmitOrderAsync(command);
    var replay = await adapter.SubmitOrderAsync(command);
    var recovered = await adapter.GetOrderAsync(command.BrokerOrderId);

    Require(first.State == BrokerResultState.Unknown, "post-accept response loss must normalize to UNKNOWN");
    Require(first.ReasonCode == "RESPONSE_LOST_AFTER_ACCEPT", "ambiguous timeout reason must be explicit");
    Require(replay == first, "blind duplicate submission must be frozen by idempotency");
    Require(adapter.AcceptedOrderCount == 1, "ambiguous timeout replay must not create another order");

    var recoveredOrder = RequireValue(recovered, BrokerResultState.Success);
    Require(recoveredOrder.State == BrokerOrderState.Filled, "query recovery must reveal authoritative paper order state");
    Require(recoveredOrder.FilledQuantity == 6m, "query recovery must preserve exactly one financial effect");
}

static async Task VerifyCancelBeforeFillAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.Open,
    });
    var command = CreateCommand(8, quantity: 4m);

    var submitted = RequireValue(await adapter.SubmitOrderAsync(command), BrokerResultState.Success);
    Require(submitted.State == BrokerOrderState.Open, "open scenario must remain OPEN before cancel");

    var cancelled = RequireValue(await adapter.CancelOrderAsync(command.BrokerOrderId), BrokerResultState.Success);
    Require(cancelled.State == BrokerOrderState.Cancelled, "cancel-before-fill must finish CANCELLED");
    Require(cancelled.FilledQuantity == 0m, "cancel-before-fill must have zero filled quantity");
}

static async Task VerifyFillCancelRaceAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.PartialFill,
        PartialFillRatio = 0.25m,
    });
    var command = CreateCommand(9, quantity: 8m);

    var partial = RequireValue(await adapter.SubmitOrderAsync(command), BrokerResultState.Success);
    Require(partial.FilledQuantity == 2m, "race setup must have authoritative partial fill");

    var cancelled = RequireValue(await adapter.CancelOrderAsync(command.BrokerOrderId), BrokerResultState.Success);
    Require(cancelled.State == BrokerOrderState.Cancelled, "cancel must resolve only the remaining paper quantity");
    Require(cancelled.FilledQuantity == 2m, "cancel acknowledgement must not erase prior fill");

    var positions = RequireValue(await adapter.GetPositionsAsync(command.AccountReference), BrokerResultState.Success);
    Require(positions.Single().Quantity == 2m, "executed portion must remain authoritative after cancel");
}

static async Task VerifyDuplicateEventAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.Open,
    });
    var command = CreateCommand(10, quantity: 5m);
    _ = RequireValue(await adapter.SubmitOrderAsync(command), BrokerResultState.Success);

    BrokerExecution execution = new(
        "PB-010-execution",
        command.BrokerOrderId,
        command.InstrumentReference,
        2m,
        100m,
        DateTimeOffset.UnixEpoch.AddMinutes(10));
    PaperBrokerEvent paperEvent = new(
        "PB-010-event",
        command.BrokerOrderId,
        BrokerOrderState.PartiallyFilled,
        execution);

    var first = adapter.ApplyPaperEvent(paperEvent);
    var duplicate = adapter.ApplyPaperEvent(paperEvent);

    Require(!first.DuplicateEvent, "first event delivery must be accepted");
    Require(duplicate.DuplicateEvent, "second event delivery must be identified as duplicate");

    var positions = RequireValue(await adapter.GetPositionsAsync(command.AccountReference), BrokerResultState.Success);
    Require(positions.Single().Quantity == 2m, "duplicate event must not duplicate financial effect");
}

static async Task VerifyOutOfOrderEventAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        SubmissionOutcome = PaperSubmissionOutcome.Open,
    });
    var command = CreateCommand(11, quantity: 5m);
    _ = RequireValue(await adapter.SubmitOrderAsync(command), BrokerResultState.Success);

    BrokerExecution execution = new(
        "PB-011-execution",
        command.BrokerOrderId,
        command.InstrumentReference,
        5m,
        100m,
        DateTimeOffset.UnixEpoch.AddMinutes(11));

    var filled = adapter.ApplyPaperEvent(new PaperBrokerEvent(
        "PB-011-filled",
        command.BrokerOrderId,
        BrokerOrderState.Filled,
        execution));
    var staleOpen = adapter.ApplyPaperEvent(new PaperBrokerEvent(
        "PB-011-stale-open",
        command.BrokerOrderId,
        BrokerOrderState.Open));

    Require(filled.Snapshot.State == BrokerOrderState.Filled, "fill event must establish terminal state");
    Require(staleOpen.Snapshot.State == BrokerOrderState.Filled, "older OPEN event must not regress FILLED state");
}

static async Task VerifyMarketClosedAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        MarketOpen = false,
    });
    var command = CreateCommand(13, quantity: 2m);

    var result = await adapter.SubmitOrderAsync(command);

    Require(result.State == BrokerResultState.Rejected, "closed market must not execute as open");
    Require(result.ReasonCode == "MARKET_CLOSED", "market-closed reason must be explicit");
    Require(adapter.AcceptedOrderCount == 0, "closed market must not create broker side effect");
}

static async Task VerifyUnavailableAsync()
{
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile
    {
        IsAvailable = false,
    });
    var command = CreateCommand(14, quantity: 2m);

    var result = await adapter.SubmitOrderAsync(command);
    var health = RequireValue(await adapter.GetHealthAsync(), BrokerResultState.Success);

    Require(result.State == BrokerResultState.RetryableFailure, "unavailable broker must fail without fabricated fill");
    Require(result.ReasonCode == "BROKER_UNAVAILABLE", "unavailable reason must be explicit");
    Require(adapter.AcceptedOrderCount == 0, "unavailable broker must not create order side effect");
    Require(!health.IsAvailable && health.Status == "UNAVAILABLE", "broker health failure must be observable");
}

static SubmitBrokerOrderCommand CreateCommand(
    int sequence,
    decimal quantity,
    string? idempotencyKey = null)
{
    return new SubmitBrokerOrderCommand(
        Guid.Parse($"00000000-0000-0000-0000-{sequence:D12}"),
        Guid.Parse($"10000000-0000-0000-0000-{sequence:D12}"),
        idempotencyKey ?? $"paper-command-{sequence:D3}",
        "paper-account",
        "PSX:TEST",
        BrokerOrderSide.Buy,
        BrokerOrderType.Limit,
        quantity,
        100m,
        BrokerTimeInForce.Day);
}

static T RequireValue<T>(BrokerOperationResult<T> result, BrokerResultState expectedState)
{
    Require(result.State == expectedState, $"expected result state {expectedState}, got {result.State}");
    return result.Value ?? throw new InvalidOperationException("expected broker result value was null");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
