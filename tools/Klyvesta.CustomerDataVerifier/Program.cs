using Klyvesta.Application.Customers;
using Klyvesta.Domain.Customers;
using Klyvesta.Infrastructure.Customers;

var failures = new List<string>();
var passes = 0;

await RunAsync("CD-001", "creates an owned customer profile workspace", async () =>
{
    var (service, store) = CreateHarness();
    var customerId = Guid.NewGuid();
    var workspace = await service.UpsertProfileAsync(customerId, customerId, 0, DefaultProfile());

    AssertEqual(1L, workspace.Revision, "first profile commit revision");
    AssertEqual(1, workspace.Profile!.Version, "first profile version");
    AssertEqual(1, store.Count, "store customer count");
});

await RunAsync("CD-002", "profile updates are versioned", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();
    var first = await service.UpsertProfileAsync(customerId, customerId, 0, DefaultProfile());
    var second = await service.UpsertProfileAsync(
        customerId,
        customerId,
        first.Revision,
        DefaultProfile() with { PrimaryGoal = "Income and growth" });

    AssertEqual(2L, second.Revision, "second profile commit revision");
    AssertEqual(2, second.Profile!.Version, "second profile version");
    AssertEqual("Income and growth", second.Profile.PrimaryGoal, "updated primary goal");
});

await RunAsync("CD-003", "cross-customer reads are denied", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();
    await service.UpsertProfileAsync(customerId, customerId, 0, DefaultProfile());

    await ExpectInvalidOperationAsync(
        () => service.GetAsync(Guid.NewGuid(), customerId).AsTask(),
        "CUSTOMER_OWNERSHIP_MISMATCH");
});

await RunAsync("CD-004", "risk score is deterministic", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();
    var workspace = await service.RecalculateRiskAsync(
        customerId,
        customerId,
        0,
        new CustomerRiskAnswers(3, 3, 4, 2));

    var risk = workspace.LatestRiskProfile!;
    AssertEqual(70, risk.Score, "deterministic risk score");
    AssertEqual(CustomerRiskBand.Growth, risk.RiskBand, "risk band");
    AssertEqual(1, risk.Version, "first risk version");
});

await RunAsync("CD-005", "risk profile history is append-only and versioned", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();
    var first = await service.RecalculateRiskAsync(
        customerId,
        customerId,
        0,
        new CustomerRiskAnswers(3, 3, 4, 2));
    var second = await service.RecalculateRiskAsync(
        customerId,
        customerId,
        first.Revision,
        new CustomerRiskAnswers(4, 4, 4, 2));

    AssertEqual(2, second.RiskProfiles.Count, "risk history count");
    AssertEqual(1, second.RiskProfiles[0].Version, "first risk version retained");
    AssertEqual(2, second.RiskProfiles[1].Version, "second risk version");
});

await RunAsync("CD-006", "risk answers outside the supported range are rejected", () =>
{
    ExpectThrows<ArgumentOutOfRangeException>(() =>
    {
        _ = new CustomerRiskAnswers(0, 3, 3, 3);
    });
    return Task.CompletedTask;
});

await RunAsync("CD-007", "goals are owned by the authenticated customer", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();
    var goalId = Guid.NewGuid();
    var workspace = await service.AddGoalAsync(
        customerId,
        customerId,
        0,
        DefaultGoal(goalId));

    AssertEqual(1, workspace.Goals.Count, "goal count");
    AssertEqual(customerId, workspace.Goals[0].CustomerId, "goal owner");
    AssertEqual(goalId, workspace.Goals[0].GoalId, "goal id");
});

await RunAsync("CD-008", "duplicate goal identifiers are rejected", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();
    var goalId = Guid.NewGuid();
    var first = await service.AddGoalAsync(customerId, customerId, 0, DefaultGoal(goalId));

    await ExpectInvalidOperationAsync(
        () => service.AddGoalAsync(customerId, customerId, first.Revision, DefaultGoal(goalId)).AsTask(),
        "CUSTOMER_GOAL_ALREADY_EXISTS");
});

await RunAsync("CD-009", "goal removal is revisioned", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();
    var goalId = Guid.NewGuid();
    var first = await service.AddGoalAsync(customerId, customerId, 0, DefaultGoal(goalId));
    var second = await service.RemoveGoalAsync(customerId, customerId, first.Revision, goalId);

    AssertEqual(0, second.Goals.Count, "goal removal");
    AssertEqual(first.Revision + 1, second.Revision, "goal removal revision");
});

await RunAsync("CD-010", "watchlist symbols are normalized", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();
    var workspace = await service.AddWatchlistSymbolAsync(customerId, customerId, 0, " ogdc ");

    AssertEqual("OGDC", workspace.Watchlist[0].Symbol, "normalized symbol");
});

await RunAsync("CD-011", "duplicate watchlist additions are idempotent", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();
    var first = await service.AddWatchlistSymbolAsync(customerId, customerId, 0, "FFC");
    var second = await service.AddWatchlistSymbolAsync(customerId, customerId, first.Revision, "ffc");

    AssertEqual(first.Revision, second.Revision, "duplicate watchlist revision");
    AssertEqual(1, second.Watchlist.Count, "duplicate watchlist count");
});

await RunAsync("CD-012", "unsupported watchlist symbols are rejected", () =>
{
    ExpectThrows<ArgumentException>(() => CustomerWatchlistEntry.NormalizeSymbol("BAD SYMBOL"));
    return Task.CompletedTask;
});

await RunAsync("CD-013", "stale service revisions are rejected", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();
    await service.UpsertProfileAsync(customerId, customerId, 0, DefaultProfile());

    await ExpectInvalidOperationAsync(
        () => service.RecalculateRiskAsync(
            customerId,
            customerId,
            0,
            new CustomerRiskAnswers(3, 3, 3, 3)).AsTask(),
        "CUSTOMER_DATA_REVISION_CONFLICT");
});

await RunAsync("CD-014", "store enforces optimistic concurrency independently", async () =>
{
    var (service, store) = CreateHarness();
    var customerId = Guid.NewGuid();
    var current = await service.UpsertProfileAsync(customerId, customerId, 0, DefaultProfile());
    var candidate = new CustomerWorkspace(
        customerId,
        current.Revision + 1,
        current.Profile,
        current.RiskProfiles,
        current.Goals,
        current.Watchlist);

    await ExpectInvalidOperationAsync(
        () => store.CommitAsync(candidate, 0).AsTask(),
        "CUSTOMER_DATA_REVISION_CONFLICT");
});

await RunAsync("CD-015", "workspace rejects foreign-owned child data", () =>
{
    var customerId = Guid.NewGuid();
    var foreignCustomerId = Guid.NewGuid();
    var foreignProfile = new CustomerProfile(
        foreignCustomerId,
        1,
        "Foreign profile",
        CustomerExperienceLevel.Beginner,
        "3-5 years",
        "Growth",
        1000m,
        DateTimeOffset.UtcNow);

    ExpectThrows<ArgumentException>(() =>
    {
        _ = new CustomerWorkspace(customerId, 1, foreignProfile);
    });
    return Task.CompletedTask;
});

await RunAsync("CD-016", "customer domain exposes no restricted identity or bank fields", () =>
{
    string[] blockedTokens =
    [
        "cnic",
        "passport",
        "iban",
        "biometric",
        "bankaccount",
        "brokercredential",
        "nationalid",
    ];

    var exposedProperties = typeof(CustomerProfile).Assembly
        .GetTypes()
        .Where(type => StringComparer.Ordinal.Equals(type.Namespace, "Klyvesta.Domain.Customers"))
        .SelectMany(type => type.GetProperties())
        .Select(property => property.Name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant())
        .ToArray();

    var violation = exposedProperties.FirstOrDefault(name => blockedTokens.Any(name.Contains));
    Assert(violation is null, $"restricted property exposed: {violation}");
    return Task.CompletedTask;
});

await RunAsync("CD-017", "missing watchlist removal fails closed", async () =>
{
    var (service, _) = CreateHarness();
    var customerId = Guid.NewGuid();

    await ExpectInvalidOperationAsync(
        () => service.RemoveWatchlistSymbolAsync(customerId, customerId, 0, "PSO").AsTask(),
        "CUSTOMER_WATCHLIST_SYMBOL_NOT_FOUND");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Customer data verification FAILED ({failures.Count}):");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($" - {failure}");
    }

    return 1;
}

Console.WriteLine($"Customer data verification PASS ({passes}/17 cases). API-independent customer ownership, versioning, goals, watchlist and restricted-PII boundaries are enforced.");
return 0;

async Task RunAsync(string id, string description, Func<Task> test)
{
    try
    {
        await test().ConfigureAwait(false);
        passes++;
        Console.WriteLine($"PASS {id}: {description}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.GetType().Name}: {exception.Message}");
    }
}

static (CustomerDataService Service, InMemoryCustomerProfileStore Store) CreateHarness()
{
    var store = new InMemoryCustomerProfileStore();
    return (new CustomerDataService(store, TimeProvider.System), store);
}

static CustomerProfileInput DefaultProfile() =>
    new(
        "Demo Investor",
        CustomerExperienceLevel.Intermediate,
        "5-7 years",
        "Long-term wealth growth",
        50_000m);

static CustomerGoalInput DefaultGoal(Guid goalId) =>
    new(
        goalId,
        "Long-term portfolio",
        5_000_000m,
        2_230_252.60m,
        new DateOnly(2031, 12, 31),
        CustomerGoalStatus.Active);

static async Task ExpectInvalidOperationAsync(Func<Task> action, string expectedMessage)
{
    try
    {
        await action().ConfigureAwait(false);
    }
    catch (InvalidOperationException exception) when (StringComparer.Ordinal.Equals(exception.Message, expectedMessage))
    {
        return;
    }

    throw new InvalidOperationException($"Expected InvalidOperationException with message {expectedMessage}.");
}

static void ExpectThrows<TException>(Action action)
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

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"ASSERTION_FAILED:{message}");
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"ASSERTION_FAILED:{message}: expected={expected}, actual={actual}");
    }
}
