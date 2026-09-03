using Klyvesta.Application.Ledger;
using Klyvesta.Domain.Ledger;
using Klyvesta.Infrastructure.Ledger;

var tests = new (string Id, Func<Task> Run)[]
{
    ("LEDGER-001 balanced exact-decimal entry posts", VerifyBalancedEntryAsync),
    ("LEDGER-002 unbalanced entry is rejected", VerifyUnbalancedRejectedAsync),
    ("LEDGER-003 single-line entry is rejected", VerifySingleLineRejectedAsync),
    ("LEDGER-004 zero amount is rejected", VerifyZeroAmountRejectedAsync),
    ("LEDGER-005 negative amount is rejected", VerifyNegativeAmountRejectedAsync),
    ("LEDGER-006 scale above eight decimals is rejected", VerifyScaleRejectedAsync),
    ("LEDGER-007 multi-currency entry balances independently", VerifyMultiCurrencyBalancedAsync),
    ("LEDGER-008 cross-currency imbalance is rejected", VerifyCrossCurrencyImbalanceAsync),
    ("LEDGER-009 posted entry and lines expose no setters", VerifyImmutableSurfaceAsync),
    ("LEDGER-010 same idempotency key and payload returns original", VerifyIdempotentRetryAsync),
    ("LEDGER-011 same idempotency key with changed payload fails", VerifyIdempotencyConflictAsync),
    ("LEDGER-012 idempotency scope separates actors", VerifyActorScopedIdempotencyAsync),
    ("LEDGER-013 idempotency scope separates operations", VerifyOperationScopedIdempotencyAsync),
    ("LEDGER-014 posting order is canonical for retries", VerifyCanonicalPostingOrderAsync),
    ("LEDGER-015 decimal point one plus point two is exact", VerifyExactDecimalArithmeticAsync),
    ("LEDGER-016 reversal mirrors debit and credit sides", VerifyReversalAsync),
    ("LEDGER-017 reversal retry is idempotent", VerifyReversalIdempotencyAsync),
    ("LEDGER-018 second independent reversal is rejected", VerifyDuplicateReversalRejectedAsync),
    ("LEDGER-019 unknown reversal target is rejected", VerifyUnknownReversalRejectedAsync),
    ("LEDGER-020 account history is append-only ordered evidence", VerifyAccountHistoryAsync),
    ("LEDGER-021 store contract exposes no update/delete mutation", VerifyStoreContractHasNoDestructiveMutationAsync),
    ("LEDGER-022 invalid currency is rejected", VerifyCurrencyValidationAsync),
    ("LEDGER-023 cancellation fails before commit", VerifyCancellationAsync),
    ("LEDGER-024 journal retains actor/correlation/idempotency evidence", VerifyEvidenceRetentionAsync),
};

var failures = new List<string>();
foreach (var (id, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"LEDGER_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"LEDGER_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Ledger assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("APPEND_ONLY: posted journals and lines expose immutable read surfaces; corrections use new reversal entries.");
Console.WriteLine("DOUBLE_ENTRY: every posted entry is balanced independently per currency using exact C# decimal arithmetic.");
Console.WriteLine("IDEMPOTENCY: retries are scoped to actor + operation + key and bind to a canonical normalized request hash.");
Console.WriteLine("NO_DATABASE_AUTHORITY: this verifier uses a non-live in-memory store; migrations/schema ownership remains with database integration.");
Console.WriteLine("NOT_LIVE: no production customer data, broker credentials, pyPSX integration, funding rail, settlement authority or real-money movement is exercised.");

return failures.Count == 0 ? 0 : 1;

static async Task VerifyBalancedEntryAsync()
{
    var (service, store) = CreateService();
    var result = await service.PostAsync(Command());
    Require(!result.WasExisting, "first post must be new");
    Require(store.Count == 1, "store must contain one immutable entry");
    Require(result.Entry.Lines.Sum(line => line.Side == LedgerSide.Debit ? line.Amount : 0m) == 100.125m, "debit total must be exact");
    Require(result.Entry.Lines.Sum(line => line.Side == LedgerSide.Credit ? line.Amount : 0m) == 100.125m, "credit total must be exact");
}

static async Task VerifyUnbalancedRejectedAsync()
{
    var (service, store) = CreateService();
    await RequireThrowsAsync<ArgumentException>(
        () => service.PostAsync(Command(creditAmount: 99m)).AsTask(),
        "unbalanced posting must fail");
    Require(store.Count == 0, "unbalanced posting must not commit");
}

static async Task VerifySingleLineRejectedAsync()
{
    var (service, store) = CreateService();
    var command = Command() with
    {
        Lines = [new LedgerPostingInput(AccountA(), LedgerSide.Debit, 1m, "PKR")],
    };
    await RequireThrowsAsync<ArgumentException>(() => service.PostAsync(command).AsTask(), "single-line journal must fail");
    Require(store.Count == 0, "invalid journal must not commit");
}

static async Task VerifyZeroAmountRejectedAsync()
{
    var (service, _) = CreateService();
    await RequireThrowsAsync<ArgumentOutOfRangeException>(
        () => service.PostAsync(Command(debitAmount: 0m, creditAmount: 0m)).AsTask(),
        "zero posting must fail");
}

static async Task VerifyNegativeAmountRejectedAsync()
{
    var (service, _) = CreateService();
    await RequireThrowsAsync<ArgumentOutOfRangeException>(
        () => service.PostAsync(Command(debitAmount: -1m, creditAmount: -1m)).AsTask(),
        "negative posting must fail");
}

static async Task VerifyScaleRejectedAsync()
{
    var (service, _) = CreateService();
    const decimal tooPrecise = 1.000000001m;
    await RequireThrowsAsync<ArgumentOutOfRangeException>(
        () => service.PostAsync(Command(debitAmount: tooPrecise, creditAmount: tooPrecise)).AsTask(),
        "amount precision beyond numeric(24,8) policy must fail");
}

static async Task VerifyMultiCurrencyBalancedAsync()
{
    var (service, store) = CreateService();
    var command = Command() with
    {
        Lines =
        [
            new LedgerPostingInput(AccountA(), LedgerSide.Debit, 10m, "PKR"),
            new LedgerPostingInput(AccountB(), LedgerSide.Credit, 10m, "PKR"),
            new LedgerPostingInput(AccountC(), LedgerSide.Debit, 5.25m, "USD"),
            new LedgerPostingInput(AccountD(), LedgerSide.Credit, 5.25m, "USD"),
        ],
    };
    var result = await service.PostAsync(command);
    Require(result.Entry.Lines.Count == 4, "balanced multi-currency journal must post all lines");
    Require(store.Count == 1, "balanced journal must commit exactly once");
}

static async Task VerifyCrossCurrencyImbalanceAsync()
{
    var (service, store) = CreateService();
    var command = Command() with
    {
        Lines =
        [
            new LedgerPostingInput(AccountA(), LedgerSide.Debit, 10m, "PKR"),
            new LedgerPostingInput(AccountB(), LedgerSide.Credit, 10m, "USD"),
        ],
    };
    await RequireThrowsAsync<ArgumentException>(() => service.PostAsync(command).AsTask(), "currencies may not offset each other");
    Require(store.Count == 0, "cross-currency imbalance must not commit");
}

static Task VerifyImmutableSurfaceAsync()
{
    var entryProperties = typeof(LedgerJournalEntry).GetProperties();
    Require(entryProperties.All(property => property.SetMethod is null), "journal entry properties must expose no setters");
    Require(typeof(LedgerPosting).GetProperties().All(property => property.SetMethod is null), "journal line properties must expose no setters");

    var entry = new LedgerJournalEntry(
        Guid.CreateVersion7(), "TEST", "corr", "actor", "operation", "key", new string('a', 64), Now(), Now(), null,
        [new LedgerPosting(AccountA(), LedgerSide.Debit, 1m, "PKR"), new LedgerPosting(AccountB(), LedgerSide.Credit, 1m, "PKR")]);
    Require(entry.Lines is not LedgerPosting[], "journal must not expose its backing array");
    return Task.CompletedTask;
}

static async Task VerifyIdempotentRetryAsync()
{
    var (service, store) = CreateService();
    var command = Command(idempotencyKey: "idem-retry");
    var first = await service.PostAsync(command);
    var second = await service.PostAsync(command with { CorrelationId = "corr-retry-2" });
    Require(!first.WasExisting, "first command must be new");
    Require(second.WasExisting, "same normalized request must return existing result");
    Require(second.Entry.EntryId == first.Entry.EntryId, "idempotent retry must return original entry");
    Require(store.Count == 1, "retry must not append a duplicate entry");
}

static async Task VerifyIdempotencyConflictAsync()
{
    var (service, store) = CreateService();
    await service.PostAsync(Command(idempotencyKey: "idem-conflict"));
    var changed = Command(idempotencyKey: "idem-conflict", debitAmount: 101m, creditAmount: 101m);
    await RequireThrowsMessageAsync(
        () => service.PostAsync(changed).AsTask(),
        "LEDGER_IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD");
    Require(store.Count == 1, "idempotency conflict must not append");
}

static async Task VerifyActorScopedIdempotencyAsync()
{
    var (service, store) = CreateService();
    var first = await service.PostAsync(Command(actorReference: "actor-a", idempotencyKey: "same-key"));
    var second = await service.PostAsync(Command(actorReference: "actor-b", idempotencyKey: "same-key"));
    Require(first.Entry.EntryId != second.Entry.EntryId, "different actors must have independent idempotency scopes");
    Require(store.Count == 2, "different actor scopes may append independently");
}

static async Task VerifyOperationScopedIdempotencyAsync()
{
    var (service, store) = CreateService();
    var first = await service.PostAsync(Command(operation: "deposit.post", idempotencyKey: "same-key"));
    var second = await service.PostAsync(Command(operation: "fee.post", idempotencyKey: "same-key"));
    Require(first.Entry.EntryId != second.Entry.EntryId, "different operations must have independent idempotency scopes");
    Require(store.Count == 2, "different operation scopes may append independently");
}

static async Task VerifyCanonicalPostingOrderAsync()
{
    var (service, store) = CreateService();
    var original = Command(idempotencyKey: "canonical-order");
    var reversedOrder = original with { Lines = original.Lines.Reverse().ToArray(), CorrelationId = "new-correlation" };
    var first = await service.PostAsync(original);
    var second = await service.PostAsync(reversedOrder);
    Require(second.WasExisting, "equivalent line ordering must hash identically");
    Require(second.Entry.EntryId == first.Entry.EntryId, "canonical retry must resolve to original entry");
    Require(store.Count == 1, "posting order changes must not duplicate economic command");
}

static async Task VerifyExactDecimalArithmeticAsync()
{
    var (service, _) = CreateService();
    var exact = 0.1m + 0.2m;
    Require(exact == 0.3m, "C# decimal arithmetic must retain exact base-10 result");
    var result = await service.PostAsync(Command(debitAmount: exact, creditAmount: 0.3m));
    Require(result.Entry.Lines[0].Amount == 0.3m, "exact decimal amount must be retained without float conversion");
}

static async Task VerifyReversalAsync()
{
    var (service, store) = CreateService();
    var original = await service.PostAsync(Command(idempotencyKey: "original"));
    var reversal = await service.ReverseAsync(
        original.Entry.EntryId, "reversal-corr", "recon-approver", "ledger.reverse", "reversal-key", Now());
    Require(reversal.Entry.ReversesEntryId == original.Entry.EntryId, "reversal must link to original entry");
    Require(reversal.Entry.Lines[0].Side == Opposite(original.Entry.Lines[0].Side), "reversal must invert first side");
    Require(reversal.Entry.Lines[1].Side == Opposite(original.Entry.Lines[1].Side), "reversal must invert second side");
    Require(store.Count == 2, "reversal must append a new entry rather than mutate original");
}

static async Task VerifyReversalIdempotencyAsync()
{
    var (service, store) = CreateService();
    var original = await service.PostAsync(Command(idempotencyKey: "reversal-idem-original"));
    var first = await service.ReverseAsync(original.Entry.EntryId, "corr-1", "actor", "ledger.reverse", "same-reversal-key", Now());
    var second = await service.ReverseAsync(original.Entry.EntryId, "corr-2", "actor", "ledger.reverse", "same-reversal-key", Now());
    Require(second.WasExisting, "same reversal retry must resolve idempotently");
    Require(first.Entry.EntryId == second.Entry.EntryId, "reversal retry must return original reversal");
    Require(store.Count == 2, "reversal retry must not create a third entry");
}

static async Task VerifyDuplicateReversalRejectedAsync()
{
    var (service, store) = CreateService();
    var original = await service.PostAsync(Command(idempotencyKey: "duplicate-reversal-original"));
    await service.ReverseAsync(original.Entry.EntryId, "corr-1", "actor", "ledger.reverse", "reversal-1", Now());
    await RequireThrowsPrefixAsync(
        () => service.ReverseAsync(original.Entry.EntryId, "corr-2", "actor", "ledger.reverse", "reversal-2", Now()).AsTask(),
        "LEDGER_ENTRY_ALREADY_REVERSED:");
    Require(store.Count == 2, "independent second reversal must not append");
}

static async Task VerifyUnknownReversalRejectedAsync()
{
    var (service, store) = CreateService();
    await RequireThrowsMessageAsync(
        () => service.ReverseAsync(Guid.CreateVersion7(), "corr", "actor", "ledger.reverse", "missing", Now()).AsTask(),
        "LEDGER_ENTRY_NOT_FOUND");
    Require(store.Count == 0, "unknown reversal target must not create entries");
}

static async Task VerifyAccountHistoryAsync()
{
    var (service, store) = CreateService();
    await service.PostAsync(Command(idempotencyKey: "history-1"));
    await service.PostAsync(Command(idempotencyKey: "history-2", operation: "deposit.post.second"));
    var history = await store.ListByAccountAsync(AccountA());
    Require(history.Count == 2, "account history must include both append-only entries");
    Require(history[0].PostedAt <= history[1].PostedAt, "account history must have deterministic chronological ordering");
}

static Task VerifyStoreContractHasNoDestructiveMutationAsync()
{
    var destructive = typeof(ILedgerJournalStore)
        .GetMethods()
        .Where(method => method.Name.Contains("Update", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase))
        .Select(method => method.Name)
        .ToArray();
    Require(destructive.Length == 0, "journal store contract must not expose update/delete/remove operations");
    return Task.CompletedTask;
}

static Task VerifyCurrencyValidationAsync()
{
    RequireThrows<ArgumentException>(
        () =>
        {
            _ = new LedgerPosting(AccountA(), LedgerSide.Debit, 1m, "PK-R");
        },
        "invalid currency must fail");
    var valid = new LedgerPosting(AccountA(), LedgerSide.Debit, 1m, "pkr");
    Require(valid.Currency == "PKR", "currency normalization must be deterministic");
    return Task.CompletedTask;
}

static async Task VerifyCancellationAsync()
{
    var (service, store) = CreateService();
    using var source = new CancellationTokenSource();
    source.Cancel();
    await RequireThrowsAsync<OperationCanceledException>(
        () => service.PostAsync(Command(idempotencyKey: "cancelled"), source.Token).AsTask(),
        "cancelled command must fail before commit");
    Require(store.Count == 0, "cancelled command must not append");
}

static async Task VerifyEvidenceRetentionAsync()
{
    var (service, _) = CreateService();
    var result = await service.PostAsync(Command(
        actorReference: "reconciliation-operator",
        operation: "financial-correction.post",
        idempotencyKey: "evidence-key",
        correlationId: "request-123"));
    Require(result.Entry.ActorReference == "reconciliation-operator", "actor evidence must be retained");
    Require(result.Entry.Operation == "financial-correction.post", "operation evidence must be retained");
    Require(result.Entry.IdempotencyKey == "evidence-key", "idempotency evidence must be retained");
    Require(result.Entry.CorrelationId == "request-123", "correlation evidence must be retained");
    Require(result.Entry.NormalizedRequestHash.Length == 64, "request hash must be SHA-256 hex evidence");
}

static (LedgerPostingService Service, InMemoryLedgerJournalStore Store) CreateService()
{
    var store = new InMemoryLedgerJournalStore();
    return (new LedgerPostingService(store, new FixedTimeProvider(Now())), store);
}

static LedgerPostCommand Command(
    decimal debitAmount = 100.125m,
    decimal creditAmount = 100.125m,
    string actorReference = "reconciliation-operator",
    string operation = "deposit.post",
    string idempotencyKey = "idem-1",
    string correlationId = "corr-1") =>
    new(
        EntryType: "CASH_POSTING",
        CorrelationId: correlationId,
        ActorReference: actorReference,
        Operation: operation,
        IdempotencyKey: idempotencyKey,
        EffectiveAt: Now(),
        Lines:
        [
            new LedgerPostingInput(AccountA(), LedgerSide.Debit, debitAmount, "PKR"),
            new LedgerPostingInput(AccountB(), LedgerSide.Credit, creditAmount, "PKR"),
        ]);

static Guid AccountA() => Guid.Parse("00000000-0000-7000-8000-000000000001");
static Guid AccountB() => Guid.Parse("00000000-0000-7000-8000-000000000002");
static Guid AccountC() => Guid.Parse("00000000-0000-7000-8000-000000000003");
static Guid AccountD() => Guid.Parse("00000000-0000-7000-8000-000000000004");
static DateTimeOffset Now() => new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
static LedgerSide Opposite(LedgerSide side) => side == LedgerSide.Debit ? LedgerSide.Credit : LedgerSide.Debit;

static async Task RequireThrowsAsync<TException>(Func<Task> action, string message)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static async Task RequireThrowsMessageAsync(Func<Task> action, string expectedMessage)
{
    try
    {
        await action();
    }
    catch (InvalidOperationException exception) when (exception.Message == expectedMessage)
    {
        return;
    }

    throw new InvalidOperationException($"Expected failure {expectedMessage}.");
}

static async Task RequireThrowsPrefixAsync(Func<Task> action, string expectedPrefix)
{
    try
    {
        await action();
    }
    catch (InvalidOperationException exception) when (exception.Message.StartsWith(expectedPrefix, StringComparison.Ordinal))
    {
        return;
    }

    throw new InvalidOperationException($"Expected failure prefix {expectedPrefix}.");
}

static void RequireThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
