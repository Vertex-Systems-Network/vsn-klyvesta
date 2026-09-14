using System.Reflection;
using System.Text.Json;
using Klyvesta.Application.Activity;
using Klyvesta.Domain.Activity;
using Klyvesta.Domain.Ledger;
using Klyvesta.Domain.Orders;

var failures = new List<string>();
var passes = 0;
var builder = new DeterministicCustomerActivityBuilder();
var customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var otherCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
var asOf = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
const string accountReference = "paper-account-1";

Check("ACT-001", "same inputs produce deterministic activity output", () =>
{
    var request = CreateRequest(
        orderEvents: new[] { CreateOrderEvidence(occurredAt: asOf.AddMinutes(-2)) },
        ledgerEvents: new[] { CreateLedgerEvidence(postedAt: asOf.AddMinutes(-1)) });
    var first = JsonSerializer.Serialize(builder.Build(request));
    var second = JsonSerializer.Serialize(builder.Build(request));
    Require(first == second, "identical evidence must produce identical serialized activity output");
});

Check("ACT-002", "cross-customer request fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(authenticatedCustomerId: otherCustomerId)),
        "CUSTOMER_ACTIVITY_SCOPE_MISMATCH");
});

Check("ACT-003", "missing authenticated customer id is rejected", () =>
{
    RequireThrows<ArgumentException>(() => builder.Build(CreateRequest(authenticatedCustomerId: Guid.Empty)));
});

Check("ACT-004", "missing target customer id is rejected", () =>
{
    RequireThrows<ArgumentException>(() => builder.Build(CreateRequest(customerIdOverride: Guid.Empty)));
});

Check("ACT-005", "blank account reference is rejected", () =>
{
    RequireThrows<ArgumentException>(() => builder.Build(CreateRequest(account: "   ")));
});

Check("ACT-006", "control characters in account reference are rejected", () =>
{
    RequireThrows<ArgumentException>(() => builder.Build(CreateRequest(account: "paper\naccount")));
});

Check("ACT-007", "foreign-owned order evidence is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(orderEvents: new[] { CreateOrderEvidence(ownerCustomerId: otherCustomerId) })),
        "CUSTOMER_ACTIVITY_ORDER_CUSTOMER_SCOPE_MISMATCH");
});

Check("ACT-008", "order account scope mismatch is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(orderEvents: new[] { CreateOrderEvidence(intent: CreateIntent(account: "other-account")) })),
        "CUSTOMER_ACTIVITY_ORDER_ACCOUNT_SCOPE_MISMATCH");
});

Check("ACT-009", "future order evidence is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(orderEvents: new[] { CreateOrderEvidence(occurredAt: asOf.AddSeconds(1)) })),
        "CUSTOMER_ACTIVITY_ORDER_TIME_INVALID");
});

Check("ACT-010", "duplicate order source events are rejected", () =>
{
    var sourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(orderEvents: new[]
        {
            CreateOrderEvidence(sourceEventId: sourceId),
            CreateOrderEvidence(sourceEventId: sourceId),
        })),
        "CUSTOMER_ACTIVITY_DUPLICATE_ORDER_EVENT");
});

Check("ACT-011", "created order intent maps to structured timeline code", () =>
{
    var timeline = builder.Build(CreateRequest(orderEvents: new[] { CreateOrderEvidence(intent: CreateIntent(OrderIntentState.Created)) }));
    Require(timeline.Items.Single().Code == CustomerActivityCode.OrderIntentCreated, "created intent must map to OrderIntentCreated");
});

Check("ACT-012", "rejected order intent maps without raw rejection reason", () =>
{
    var timeline = builder.Build(CreateRequest(orderEvents: new[] { CreateOrderEvidence(intent: CreateIntent(OrderIntentState.Rejected)) }));
    var item = timeline.Items.Single();
    Require(item.Code == CustomerActivityCode.OrderRejected, "rejected intent must map to OrderRejected");
    Require(item.InstrumentReference == "HBL", "safe instrument identity should be retained");
});

Check("ACT-013", "broker evidence requires execution-created intent", () =>
{
    var intent = CreateIntent(OrderIntentState.Approved);
    var broker = new ManagedBrokerOrder(intent.PlannedBrokerOrderId, intent.Id, intent.Quantity);
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(orderEvents: new[] { CreateOrderEvidence(intent: intent, brokerOrder: broker) })),
        "CUSTOMER_ACTIVITY_BROKER_STATE_WITHOUT_EXECUTION");
});

Check("ACT-014", "broker order must match the planned order identity", () =>
{
    var intent = CreateIntent(OrderIntentState.ExecutionCreated);
    var broker = new ManagedBrokerOrder(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), intent.Id, intent.Quantity);
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(orderEvents: new[] { CreateOrderEvidence(intent: intent, brokerOrder: broker) })),
        "CUSTOMER_ACTIVITY_BROKER_ORDER_SCOPE_MISMATCH");
});

Check("ACT-015", "partial fill maps quantity from canonical order evidence", () =>
{
    var intent = CreateIntent(OrderIntentState.ExecutionCreated);
    var broker = CreateBrokerOrder(intent, ManagedBrokerOrderState.PartiallyFilled, asOf.AddMinutes(-2));
    var timeline = builder.Build(CreateRequest(orderEvents: new[]
    {
        CreateOrderEvidence(intent: intent, brokerOrder: broker, occurredAt: asOf.AddMinutes(-1)),
    }));
    var item = timeline.Items.Single();
    Require(item.Code == CustomerActivityCode.OrderPartiallyFilled, "partial fill code mismatch");
    Require(item.Quantity == 10m && item.FilledQuantity == 4m, "requested and filled quantities must remain exact decimals");
});

Check("ACT-016", "full fill maps to filled state", () =>
{
    var intent = CreateIntent(OrderIntentState.ExecutionCreated);
    var broker = CreateBrokerOrder(intent, ManagedBrokerOrderState.Filled, asOf.AddMinutes(-2));
    var item = builder.Build(CreateRequest(orderEvents: new[]
    {
        CreateOrderEvidence(intent: intent, brokerOrder: broker, occurredAt: asOf.AddMinutes(-1)),
    })).Items.Single();
    Require(item.Code == CustomerActivityCode.OrderFilled && item.FilledQuantity == 10m, "full fill must be represented exactly");
});

Check("ACT-017", "future execution evidence fails closed", () =>
{
    var intent = CreateIntent(OrderIntentState.ExecutionCreated);
    var broker = new ManagedBrokerOrder(intent.PlannedBrokerOrderId, intent.Id, intent.Quantity);
    broker.ApplyExecution(new ManagedExecution("future-execution", 4m, 100m, asOf.AddMinutes(1)));
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(orderEvents: new[]
        {
            CreateOrderEvidence(intent: intent, brokerOrder: broker, occurredAt: asOf),
        })),
        "CUSTOMER_ACTIVITY_EXECUTION_EVIDENCE_INVALID");
});

Check("ACT-018", "foreign-owned ledger evidence is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(ledgerEvents: new[] { CreateLedgerEvidence(ownerCustomerId: otherCustomerId) })),
        "CUSTOMER_ACTIVITY_LEDGER_CUSTOMER_SCOPE_MISMATCH");
});

Check("ACT-019", "missing ledger account metadata fails closed", () =>
{
    var evidence = CreateLedgerEvidence();
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(ledgerEvents: new[]
        {
            evidence with { Accounts = new[] { evidence.Accounts[0] } },
        })),
        "CUSTOMER_ACTIVITY_LEDGER_ACCOUNT_EVIDENCE_MISSING");
});

Check("ACT-020", "ledger account owner mismatch is rejected", () =>
{
    var evidence = CreateLedgerEvidence(accountOwnerCustomerId: otherCustomerId);
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(ledgerEvents: new[] { evidence })),
        "CUSTOMER_ACTIVITY_LEDGER_OWNER_SCOPE_MISMATCH");
});

Check("ACT-021", "ledger account reference mismatch is rejected", () =>
{
    var evidence = CreateLedgerEvidence(targetAccountReference: "other-account");
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(ledgerEvents: new[] { evidence })),
        "CUSTOMER_ACTIVITY_LEDGER_ACCOUNT_SCOPE_MISMATCH");
});

Check("ACT-022", "ledger posting currency must match account currency", () =>
{
    var evidence = CreateLedgerEvidence(counterCurrency: "USD");
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(ledgerEvents: new[] { evidence })),
        "CUSTOMER_ACTIVITY_LEDGER_CURRENCY_MISMATCH");
});

Check("ACT-023", "duplicate ledger entry evidence is rejected", () =>
{
    var evidence = CreateLedgerEvidence();
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(ledgerEvents: new[] { evidence, evidence })),
        "CUSTOMER_ACTIVITY_DUPLICATE_LEDGER_EVENT");
});

Check("ACT-024", "customer ledger debit impact is derived from canonical postings", () =>
{
    var item = builder.Build(CreateRequest(ledgerEvents: new[] { CreateLedgerEvidence(amount: 125.50m) })).Items.Single();
    Require(item.Source == CustomerActivitySource.Ledger, "ledger source mismatch");
    Require(item.Code == CustomerActivityCode.LedgerPosted, "ledger posting code mismatch");
    Require(item.Amount == 125.50m && item.Currency == "PKR", "ledger amount/currency must remain exact");
    Require(item.Flow == CustomerActivityFlow.Debit, "customer posting side must be retained without interpretation");
});

Check("ACT-025", "ledger reversal is structurally distinguished", () =>
{
    var item = builder.Build(CreateRequest(ledgerEvents: new[] { CreateLedgerEvidence(reversal: true) })).Items.Single();
    Require(item.Code == CustomerActivityCode.LedgerReversal, "reversal must have an explicit code");
});

Check("ACT-026", "timeline sorts newest evidence first", () =>
{
    var olderOrder = CreateOrderEvidence(
        sourceEventId: Guid.Parse("10000000-0000-0000-0000-000000000001"),
        occurredAt: asOf.AddMinutes(-5));
    var newerLedger = CreateLedgerEvidence(
        entryId: Guid.Parse("20000000-0000-0000-0000-000000000001"),
        postedAt: asOf.AddMinutes(-1));
    var timeline = builder.Build(CreateRequest(orderEvents: new[] { olderOrder }, ledgerEvents: new[] { newerLedger }));
    Require(timeline.Items[0].Source == CustomerActivitySource.Ledger, "newest event must appear first");
    Require(timeline.Items[1].Source == CustomerActivitySource.Order, "older event must follow");
});

Check("ACT-027", "equal timestamps use deterministic source and id tie-breaks", () =>
{
    var orderA = CreateOrderEvidence(
        sourceEventId: Guid.Parse("10000000-0000-0000-0000-000000000002"),
        occurredAt: asOf.AddMinutes(-1));
    var orderB = CreateOrderEvidence(
        sourceEventId: Guid.Parse("10000000-0000-0000-0000-000000000001"),
        occurredAt: asOf.AddMinutes(-1));
    var ledger = CreateLedgerEvidence(
        entryId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
        postedAt: asOf.AddMinutes(-1));
    var timeline = builder.Build(CreateRequest(orderEvents: new[] { orderA, orderB }, ledgerEvents: new[] { ledger }));
    Require(timeline.Items[0].Source == CustomerActivitySource.Order, "order source must win the stable source tie-break");
    Require(timeline.Items[0].SourceEventId == orderB.SourceEventId, "order ids must then sort deterministically");
    Require(timeline.Items[2].Source == CustomerActivitySource.Ledger, "ledger source must follow equal-time order evidence");
});

Check("ACT-028", "account reference normalization is deterministic", () =>
{
    var timeline = builder.Build(CreateRequest(account: "  paper-account-1  "));
    Require(timeline.AccountReference == accountReference, "account reference must be trimmed deterministically");
});

Check("ACT-029", "activity authority is permanently read-only paper", () =>
{
    var timeline = builder.Build(CreateRequest());
    Require(timeline.Authority == CustomerActivityAuthority.ReadOnlyPaper, "activity output must not carry execution authority");
});

Check("ACT-030", "public output schema excludes restricted and provider authority fields", () =>
{
    var forbidden = new[]
    {
        "password", "cnic", "passport", "iban", "bank", "provider", "broker", "external",
        "actor", "idempotency", "hash", "credential", "token", "secret", "orderplacement",
    };
    foreach (var type in new[] { typeof(CustomerActivityTimeline), typeof(CustomerActivityItem) })
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var normalized = property.Name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            Require(!forbidden.Any(normalized.Contains), $"restricted output field exposed: {type.Name}.{property.Name}");
        }
    }
});

Check("ACT-031", "raw broker and ledger internal strings do not leak into timeline output", () =>
{
    var intent = CreateIntent(OrderIntentState.ExecutionCreated);
    var broker = new ManagedBrokerOrder(intent.PlannedBrokerOrderId, intent.Id, intent.Quantity);
    broker.BeginSubmission();
    broker.ObserveState(ManagedBrokerOrderState.Open, "provider-order-secret", "broker-reason-secret");
    var order = CreateOrderEvidence(intent: intent, brokerOrder: broker, occurredAt: asOf.AddMinutes(-1));
    var ledger = CreateLedgerEvidence(
        postedAt: asOf.AddMinutes(-2),
        actorReference: "actor-secret",
        idempotencyKey: "idempotency-secret",
        normalizedRequestHash: "hash-secret");
    var json = JsonSerializer.Serialize(builder.Build(CreateRequest(orderEvents: new[] { order }, ledgerEvents: new[] { ledger })));
    foreach (var secret in new[] { "provider-order-secret", "broker-reason-secret", "actor-secret", "idempotency-secret", "hash-secret" })
    {
        Require(!json.Contains(secret, StringComparison.Ordinal), $"internal string leaked into timeline: {secret}");
    }
});

Check("ACT-032", "builder has no broker provider database or execution constructor dependency", () =>
{
    var constructors = typeof(DeterministicCustomerActivityBuilder).GetConstructors();
    Require(constructors.Length == 1, "builder should expose one deterministic constructor");
    Require(constructors[0].GetParameters().Length == 0, "builder must not depend on runtime broker/provider/database/execution services");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Customer activity verifier FAIL ({passes}/{passes + failures.Count}).");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine($"Customer activity verifier PASS ({passes}/{passes}).");
Console.WriteLine("CUSTOMER_SCOPE: authenticated customer, order ownership evidence, account reference and ledger ownership metadata are validated before timeline output.");
Console.WriteLine("DETERMINISTIC_TIMELINE: events sort newest-first with stable source/id tie-breaks and exact decimal quantities/amounts.");
Console.WriteLine("SANITIZED_OUTPUT: broker external IDs/reasons and ledger actor/idempotency/hash internals are not copied into user-facing activity records.");
Console.WriteLine("READ_ONLY_PAPER: activity output cannot place orders, move money, dispatch providers or create broker/live authority.");
Console.WriteLine("NOT_LIVE: no database, production PII, provider credentials, pyPSX transport or real-money path is exercised.");
return 0;

CustomerActivityRequest CreateRequest(
    Guid? authenticatedCustomerId = null,
    Guid? customerIdOverride = null,
    string account = accountReference,
    IReadOnlyList<CustomerOrderActivityEvidence>? orderEvents = null,
    IReadOnlyList<CustomerLedgerActivityEvidence>? ledgerEvents = null)
{
    return new CustomerActivityRequest(
        authenticatedCustomerId ?? customerId,
        customerIdOverride ?? customerId,
        account,
        asOf,
        orderEvents ?? Array.Empty<CustomerOrderActivityEvidence>(),
        ledgerEvents ?? Array.Empty<CustomerLedgerActivityEvidence>());
}

CustomerOrderActivityEvidence CreateOrderEvidence(
    Guid? sourceEventId = null,
    Guid? ownerCustomerId = null,
    DateTimeOffset? occurredAt = null,
    OrderIntent? intent = null,
    ManagedBrokerOrder? brokerOrder = null)
{
    return new CustomerOrderActivityEvidence(
        sourceEventId ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
        ownerCustomerId ?? customerId,
        occurredAt ?? asOf.AddMinutes(-1),
        intent ?? CreateIntent(OrderIntentState.Created),
        brokerOrder);
}

OrderIntent CreateIntent(
    OrderIntentState state = OrderIntentState.Created,
    string account = accountReference,
    string instrument = "HBL",
    Guid? intentId = null,
    Guid? plannedBrokerOrderId = null)
{
    var intent = new OrderIntent(
        intentId ?? Guid.Parse("33333333-3333-3333-3333-333333333333"),
        plannedBrokerOrderId ?? Guid.Parse("44444444-4444-4444-4444-444444444444"),
        "idem-order-1",
        account,
        instrument,
        10m);

    if (state == OrderIntentState.Created)
    {
        return intent;
    }

    intent.StartValidation();
    if (state == OrderIntentState.Validating)
    {
        return intent;
    }

    if (state == OrderIntentState.Rejected)
    {
        intent.RecordValidation(new OrderValidationEvidence(false, "POLICY_REJECT", "paper-v1", "evidence-1"), null);
        return intent;
    }

    intent.RecordValidation(
        new OrderValidationEvidence(true, "ALLOW", "paper-v1", "evidence-1"),
        new OrderReservation(Guid.Parse("55555555-5555-5555-5555-555555555555"), ReservationKind.Cash, 1000m));
    if (state == OrderIntentState.Approved)
    {
        return intent;
    }

    if (state == OrderIntentState.Cancelled)
    {
        intent.CancelBeforeBrokerSubmission();
        return intent;
    }

    if (state == OrderIntentState.Expired)
    {
        intent.ExpireBeforeBrokerSubmission();
        return intent;
    }

    intent.QueueExecution();
    if (state == OrderIntentState.ExecutionPending)
    {
        return intent;
    }

    intent.MarkExecutionCreated();
    return intent;
}

ManagedBrokerOrder CreateBrokerOrder(OrderIntent intent, ManagedBrokerOrderState state, DateTimeOffset tradeAt)
{
    var broker = new ManagedBrokerOrder(intent.PlannedBrokerOrderId, intent.Id, intent.Quantity);
    switch (state)
    {
        case ManagedBrokerOrderState.PendingSubmit:
            return broker;
        case ManagedBrokerOrderState.Submitting:
            broker.BeginSubmission();
            return broker;
        case ManagedBrokerOrderState.Submitted:
            broker.BeginSubmission();
            broker.ObserveState(ManagedBrokerOrderState.Submitted, "external-order-1");
            return broker;
        case ManagedBrokerOrderState.Open:
            broker.BeginSubmission();
            broker.ObserveState(ManagedBrokerOrderState.Open, "external-order-1");
            return broker;
        case ManagedBrokerOrderState.PartiallyFilled:
            broker.ApplyExecution(new ManagedExecution("execution-1", 4m, 100m, tradeAt));
            return broker;
        case ManagedBrokerOrderState.Filled:
            broker.ApplyExecution(new ManagedExecution("execution-1", 10m, 100m, tradeAt));
            return broker;
        case ManagedBrokerOrderState.CancelPending:
            broker.BeginSubmission();
            broker.ObserveState(ManagedBrokerOrderState.Open, "external-order-1");
            broker.BeginCancel();
            return broker;
        case ManagedBrokerOrderState.Cancelled:
            broker.BeginSubmission();
            broker.ObserveState(ManagedBrokerOrderState.Open, "external-order-1");
            broker.ObserveState(ManagedBrokerOrderState.Cancelled, "external-order-1");
            return broker;
        case ManagedBrokerOrderState.Rejected:
            broker.BeginSubmission();
            broker.ObserveState(ManagedBrokerOrderState.Rejected, "external-order-1", "REJECTED");
            return broker;
        case ManagedBrokerOrderState.Unknown:
            broker.BeginSubmission();
            broker.MarkUnknown("TIMEOUT", "external-order-1");
            return broker;
        default:
            throw new ArgumentOutOfRangeException(nameof(state), state, null);
    }
}

CustomerLedgerActivityEvidence CreateLedgerEvidence(
    Guid? entryId = null,
    Guid? ownerCustomerId = null,
    Guid? accountOwnerCustomerId = null,
    string targetAccountReference = accountReference,
    string counterCurrency = "PKR",
    decimal amount = 100m,
    DateTimeOffset? postedAt = null,
    bool reversal = false,
    string actorReference = "system-paper",
    string idempotencyKey = "ledger-idem-1",
    string normalizedRequestHash = "ledger-hash-1")
{
    var customerAccountId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    var clearingAccountId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    var owner = (accountOwnerCustomerId ?? customerId).ToString("D");
    var accounts = new[]
    {
        new LedgerAccount(customerAccountId, targetAccountReference, "PKR", LedgerAccountType.CustomerCash, owner),
        new LedgerAccount(clearingAccountId, "paper-clearing", counterCurrency, LedgerAccountType.Clearing),
    };
    var time = postedAt ?? asOf.AddMinutes(-1);
    var entry = new LedgerJournalEntry(
        entryId ?? Guid.Parse("88888888-8888-8888-8888-888888888888"),
        "PAPER_ACTIVITY",
        "correlation-1",
        actorReference,
        "paper-posting",
        idempotencyKey,
        normalizedRequestHash,
        time,
        time,
        reversal ? Guid.Parse("99999999-9999-9999-9999-999999999999") : null,
        new[]
        {
            new LedgerPosting(customerAccountId, LedgerSide.Debit, amount, "PKR"),
            new LedgerPosting(clearingAccountId, LedgerSide.Credit, amount, "PKR"),
        });
    return new CustomerLedgerActivityEvidence(ownerCustomerId ?? customerId, entry, accounts);
}

void Check(string id, string description, Action assertion)
{
    try
    {
        assertion();
        passes++;
        Console.WriteLine($"PASS {id}: {description}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {id}: {description} — {exception.GetType().Name}: {exception.Message}");
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void RequireThrows<TException>(Action action, string? expectedMessage = null)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException exception)
    {
        if (expectedMessage is not null && !exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected exception message containing '{expectedMessage}', got '{exception.Message}'.");
        }

        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}
