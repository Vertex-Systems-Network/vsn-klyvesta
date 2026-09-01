using Klyvesta.Application.Brokerage;
using Klyvesta.Application.Orders;
using Klyvesta.Application.Portfolios;
using Klyvesta.Domain.Orders;
using Klyvesta.Domain.Portfolios;
using Klyvesta.Infrastructure.Brokerage.Paper;

var tests = new (string Id, Func<Task> Run)[]
{
    ("PORT-001 projection rebuild is deterministic", VerifyDeterministicRebuildAsync),
    ("PORT-002 duplicate source and execution evidence is idempotent", VerifyDeduplicationAsync),
    ("PORT-003 weighted-average paper cost basis is exact", VerifyCostBasisAsync),
    ("PORT-004 matching broker snapshot reconciles cleanly", VerifyMatchingReconciliationAsync),
    ("PORT-005 cash mismatch is classified without silent correction", VerifyCashMismatchAsync),
    ("PORT-006 missing projected broker position is classified", VerifyMissingBrokerPositionAsync),
    ("PORT-007 unexpected broker position is classified", VerifyUnexpectedBrokerPositionAsync),
    ("PORT-008 position quantity mismatch is classified", VerifyQuantityMismatchAsync),
    ("PORT-009 critical mismatch blocks OMS submit before PaperBroker", VerifyExecutionHoldAsync),
    ("PORT-010 matching subsequent evidence explicitly clears hold", VerifyHoldClearAsync),
};

var failures = new List<string>();
foreach (var (id, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"PORTFOLIO_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"PORTFOLIO_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Portfolio assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("STACKED_ON_P1_07: this verifier requires the non-live PaperBroker + OMS slices from draft PRs #68 and #71.");
Console.WriteLine("READ_MODEL_ONLY: projection/reconciliation does not replace the future authoritative double-entry ledger or broker evidence.");
Console.WriteLine("NOT_ACCEPTED_AS_FULL_P1: ledger, authorization, deterministic risk/compliance, AI shadow and remaining exit-gate evidence are separate work.");
Console.WriteLine("NOT_LIVE: no pyPSX mapping, live credentials, production customer data or real-money authority is exercised.");

return failures.Count == 0 ? 0 : 1;

static Task VerifyDeterministicRebuildAsync()
{
    const string account = "paper-portfolio-001";
    var events = new[]
    {
        Opening(1, account, 0, 1_000m),
        Execution(2, account, 1, "exec-001-a", "AAA", PortfolioTradeSide.Buy, 2m, 100m),
        Execution(3, account, 2, "exec-001-b", "BBB", PortfolioTradeSide.Buy, 1m, 200m),
    };

    var first = PortfolioProjection.Rebuild(account, events);
    var second = PortfolioProjection.Rebuild(account, events.Reverse());

    Require(first == second, "rebuild from identical source events must be deterministic regardless of input enumeration order");
    Require(first.Cash == 600m, "rebuild cash must be exact");
    Require(first.Positions.Count == 2, "rebuild must contain both projected positions");
    return Task.CompletedTask;
}

static Task VerifyDeduplicationAsync()
{
    const string account = "paper-portfolio-002";
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile { StartingCashPerAccount = 1_000m });
    var service = new PortfolioProjectionService(adapter, FixedClock);
    var opening = Opening(10, account, 0, 1_000m);
    var execution = Execution(11, account, 1, "exec-002", "AAA", PortfolioTradeSide.Buy, 2m, 100m);

    _ = service.AppendSourceEvent(opening);
    var once = service.AppendSourceEvent(execution);
    var duplicateEvent = service.AppendSourceEvent(execution);
    var duplicateExecution = service.AppendSourceEvent(
        Execution(12, account, 2, "exec-002", "AAA", PortfolioTradeSide.Buy, 2m, 100m));

    Require(once.Cash == 800m && once.Positions.Single().Quantity == 2m, "first execution must affect projection once");
    Require(duplicateEvent == once, "same source event replay must be idempotent");
    Require(duplicateExecution.Cash == 800m, "same execution id under a new source event must not duplicate cash effect");
    Require(duplicateExecution.Positions.Single().Quantity == 2m, "same execution id must not duplicate position effect");
    Require(duplicateExecution.UniqueExecutionCount == 1, "execution identity count must remain one");
    Require(duplicateExecution.UniqueSourceEventCount == 3, "distinct duplicate-delivery source event remains observable without financial duplication");
    return Task.CompletedTask;
}

static Task VerifyCostBasisAsync()
{
    const string account = "paper-portfolio-003";
    var snapshot = PortfolioProjection.Rebuild(account, new[]
    {
        Opening(20, account, 0, 1_000_000m),
        Execution(21, account, 1, "exec-003-a", "AAA", PortfolioTradeSide.Buy, 10m, 100m),
        Execution(22, account, 2, "exec-003-b", "AAA", PortfolioTradeSide.Buy, 10m, 120m),
        Execution(23, account, 3, "exec-003-c", "AAA", PortfolioTradeSide.Sell, 5m, 130m),
    });

    var position = snapshot.Positions.Single();
    Require(position.Quantity == 15m, "sell must reduce projected quantity exactly");
    Require(position.AverageCost == 110m, "paper weighted-average cost basis must remain 110 after partial sell");
    Require(snapshot.Cash == 998_450m, "paper cash projection must be exact after buy/buy/sell sequence");
    return Task.CompletedTask;
}

static async Task VerifyMatchingReconciliationAsync()
{
    const string account = "paper-portfolio-004";
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile { StartingCashPerAccount = 1_000m });
    var service = new PortfolioProjectionService(adapter, FixedClock);
    _ = service.AppendSourceEvent(Opening(30, account, 0, 1_000m));

    var brokerSnapshot = await SubmitPaperBuyAsync(adapter, account, seed: 30, quantity: 2m, price: 100m);
    var execution = brokerSnapshot.Executions.Single();
    _ = service.AppendSourceEvent(Execution(
        31,
        account,
        1,
        execution.ExecutionId,
        execution.InstrumentReference,
        PortfolioTradeSide.Buy,
        execution.Quantity,
        execution.Price));

    var report = await service.ReconcileAsync(account);

    Require(report.IsMatch, "matching projected and broker evidence must reconcile cleanly");
    Require(report.Mismatches.Count == 0, "matching reconciliation must contain zero mismatches");
    Require(!service.GetHold(account).IsHeld, "matching reconciliation must not leave execution hold active");
}

static async Task VerifyCashMismatchAsync()
{
    const string account = "paper-portfolio-005";
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile { StartingCashPerAccount = 900m });
    var service = new PortfolioProjectionService(adapter, FixedClock);
    var before = service.AppendSourceEvent(Opening(40, account, 0, 1_000m));

    var report = await service.ReconcileAsync(account);
    var after = service.GetProjection(account);

    Require(!report.IsMatch, "cash mismatch must fail reconciliation");
    Require(report.Mismatches.Any(item => item.Kind == PortfolioMismatchKind.CashMismatch), "cash mismatch must be classified explicitly");
    Require(before == after, "reconciliation comparison must not silently rewrite projection");
    Require(service.GetHold(account).IsHeld, "critical cash mismatch must activate execution hold");
}

static async Task VerifyMissingBrokerPositionAsync()
{
    const string account = "paper-portfolio-006";
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile { StartingCashPerAccount = 1_000m });
    var service = new PortfolioProjectionService(adapter, FixedClock);
    _ = service.AppendSourceEvent(Opening(50, account, 0, 1_000m));
    _ = service.AppendSourceEvent(Execution(51, account, 1, "exec-006", "AAA", PortfolioTradeSide.Buy, 1m, 100m));

    var report = await service.ReconcileAsync(account);

    Require(report.Mismatches.Any(item => item.Kind == PortfolioMismatchKind.MissingBrokerPosition), "projected position absent from broker evidence must be classified");
    Require(service.GetProjection(account).Positions.Single().Quantity == 1m, "mismatch comparison must preserve projected position");
}

static async Task VerifyUnexpectedBrokerPositionAsync()
{
    const string account = "paper-portfolio-007";
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile { StartingCashPerAccount = 1_000m });
    var service = new PortfolioProjectionService(adapter, FixedClock);
    _ = service.AppendSourceEvent(Opening(60, account, 0, 1_000m));
    _ = await SubmitPaperBuyAsync(adapter, account, seed: 60, quantity: 1m, price: 100m);

    var report = await service.ReconcileAsync(account);

    Require(report.Mismatches.Any(item => item.Kind == PortfolioMismatchKind.UnexpectedBrokerPosition), "broker-only position must be classified explicitly");
    Require(service.GetProjection(account).Positions.Count == 0, "reconciliation must not silently import broker-only position into projection");
}

static async Task VerifyQuantityMismatchAsync()
{
    const string account = "paper-portfolio-008";
    var adapter = new PaperBrokerAdapter(new PaperBrokerProfile { StartingCashPerAccount = 1_000m });
    var service = new PortfolioProjectionService(adapter, FixedClock);
    _ = service.AppendSourceEvent(Opening(70, account, 0, 1_000m));
    _ = service.AppendSourceEvent(Execution(71, account, 1, "exec-projected-008", "SYNTH", PortfolioTradeSide.Buy, 2m, 100m));
    _ = await SubmitPaperBuyAsync(adapter, account, seed: 70, quantity: 3m, price: 100m);

    var report = await service.ReconcileAsync(account);

    Require(report.Mismatches.Any(item => item.Kind == PortfolioMismatchKind.PositionQuantityMismatch), "different broker/projected quantities must be classified");
    var mismatch = report.Mismatches.Single(item => item.Kind == PortfolioMismatchKind.PositionQuantityMismatch);
    Require(mismatch.ProjectedValue == 2m && mismatch.BrokerValue == 3m, "quantity mismatch must retain both values as evidence");
}

static async Task VerifyExecutionHoldAsync()
{
    const string account = "paper-portfolio-009";
    var inner = new PaperBrokerAdapter(new PaperBrokerProfile { StartingCashPerAccount = 1_000m });
    var portfolio = new PortfolioProjectionService(inner, FixedClock);
    _ = portfolio.AppendSourceEvent(Opening(80, account, 0, 1_000m));
    _ = await SubmitPaperBuyAsync(inner, account, seed: 80, quantity: 1m, price: 100m);
    var mismatch = await portfolio.ReconcileAsync(account);

    Require(!mismatch.IsMatch && portfolio.GetHold(account).IsHeld, "stale projection must activate hold before OMS test");
    var acceptedBeforeOms = inner.AcceptedOrderCount;
    var guardedAdapter = new ExecutionHoldBrokerAdapter(inner, portfolio, FixedClock);
    using var oms = new OrderManagementService(guardedAdapter, FixedClock);
    var request = CreateOmsRequest(account, seed: 80);
    await PrepareApprovedOmsAsync(oms, request);

    var blocked = await oms.ExecuteAsync(request.OrderIntentId);

    Require(blocked.BrokerOrderState == ManagedBrokerOrderState.PendingSubmit, "pre-side-effect hold must keep OMS broker order retryable");
    Require(blocked.RemainingReservationAmount == 100m, "hold must retain unresolved reservation");
    Require(inner.AcceptedOrderCount == acceptedBeforeOms, "execution hold must prevent forwarding a new submit to inner PaperBroker");
}

static async Task VerifyHoldClearAsync()
{
    const string account = "paper-portfolio-010";
    var inner = new PaperBrokerAdapter(new PaperBrokerProfile { StartingCashPerAccount = 1_000m });
    var portfolio = new PortfolioProjectionService(inner, FixedClock);
    _ = portfolio.AppendSourceEvent(Opening(90, account, 0, 1_000m));

    var external = await SubmitPaperBuyAsync(inner, account, seed: 90, quantity: 1m, price: 100m);
    var mismatch = await portfolio.ReconcileAsync(account);
    Require(!mismatch.IsMatch && portfolio.GetHold(account).IsHeld, "broker change absent from projection must activate hold");

    var guardedAdapter = new ExecutionHoldBrokerAdapter(inner, portfolio, FixedClock);
    using var oms = new OrderManagementService(guardedAdapter, FixedClock);
    var request = CreateOmsRequest(account, seed: 90);
    await PrepareApprovedOmsAsync(oms, request);
    var blocked = await oms.ExecuteAsync(request.OrderIntentId);
    var acceptedWhileHeld = inner.AcceptedOrderCount;
    Require(blocked.BrokerOrderState == ManagedBrokerOrderState.PendingSubmit, "held OMS order must remain retryable");

    var execution = external.Executions.Single();
    _ = portfolio.AppendSourceEvent(Execution(
        91,
        account,
        1,
        execution.ExecutionId,
        execution.InstrumentReference,
        PortfolioTradeSide.Buy,
        execution.Quantity,
        execution.Price));
    var matched = await portfolio.ReconcileAsync(account);

    Require(matched.IsMatch, "matching subsequent source evidence must reconcile explicitly");
    Require(!portfolio.GetHold(account).IsHeld, "matching reconciliation must clear hold");

    var allowed = await oms.ExecuteAsync(request.OrderIntentId);
    Require(allowed.BrokerOrderState == ManagedBrokerOrderState.Filled, "same pending OMS broker order must proceed after hold clears");
    Require(inner.AcceptedOrderCount == acceptedWhileHeld + 1, "cleared hold must allow exactly one forwarded PaperBroker submit");
}

static PortfolioProjectionEvent Opening(int seed, string account, long sequence, decimal cash) =>
    PortfolioProjectionEvent.OpeningCash(
        CreateGuid(seed),
        account,
        sequence,
        DateTimeOffset.UnixEpoch.AddSeconds(sequence),
        cash,
        $"synthetic-opening-{seed:D3}");

static PortfolioProjectionEvent Execution(
    int seed,
    string account,
    long sequence,
    string executionId,
    string instrument,
    PortfolioTradeSide side,
    decimal quantity,
    decimal price) =>
    PortfolioProjectionEvent.Execution(
        CreateGuid(seed),
        account,
        sequence,
        DateTimeOffset.UnixEpoch.AddSeconds(sequence),
        executionId,
        instrument,
        side,
        quantity,
        price,
        $"synthetic-execution-evidence-{seed:D3}");

static async Task<BrokerOrderSnapshot> SubmitPaperBuyAsync(
    PaperBrokerAdapter adapter,
    string account,
    int seed,
    decimal quantity,
    decimal price)
{
    SubmitBrokerOrderCommand command = new(
        CreateGuid(10_000 + seed),
        CreateGuid(20_000 + seed),
        $"paper-direct-{seed:D3}",
        account,
        "SYNTH",
        BrokerOrderSide.Buy,
        BrokerOrderType.Limit,
        quantity,
        price,
        BrokerTimeInForce.Day);

    var result = await adapter.SubmitOrderAsync(command);
    Require(result.State == BrokerResultState.Success, $"synthetic PaperBroker setup submit must succeed, got {result.State}");
    return result.Value ?? throw new InvalidOperationException("Synthetic PaperBroker setup submit did not return order evidence.");
}

static OmsOrderRequest CreateOmsRequest(string account, int seed) => new(
    CreateGuid(30_000 + seed),
    CreateGuid(40_000 + seed),
    $"portfolio-oms-{seed:D3}",
    account,
    "SYNTH",
    BrokerOrderSide.Buy,
    BrokerOrderType.Limit,
    1m,
    100m,
    BrokerTimeInForce.Day);

static async Task PrepareApprovedOmsAsync(OrderManagementService oms, OmsOrderRequest request)
{
    await oms.CreateIntentAsync(request);
    await oms.BeginValidationAsync(request.OrderIntentId);
    await oms.RecordValidationAsync(
        request.OrderIntentId,
        new OrderValidationEvidence(true, "ALLOW_SYNTHETIC_POLICY", "synthetic-policy-v1", $"synthetic-portfolio-evidence-{request.OrderIntentId:N}"),
        new OmsReservationSpec(CreateGuid(50_000 + ParseSeed(request.OrderIntentId)), ReservationKind.Cash, 100m));
    await oms.QueueForExecutionAsync(request.OrderIntentId);
}

static DateTimeOffset FixedClock() => DateTimeOffset.UnixEpoch.AddHours(2);

static Guid CreateGuid(int seed) => Guid.Parse($"00000000-0000-0000-0000-{seed:D12}");

static int ParseSeed(Guid id) => int.Parse(id.ToString("N")[20..], System.Globalization.CultureInfo.InvariantCulture);

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
