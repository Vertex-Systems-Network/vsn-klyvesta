using System.Reflection;
using Klyvesta.Application.AlertRules;
using Klyvesta.Application.Preferences;
using Klyvesta.Domain.AlertRules;
using Klyvesta.Domain.Notifications;
using Klyvesta.Infrastructure.AlertRules;
using Klyvesta.Infrastructure.Preferences;

var failures = new List<string>();
var passes = 0;
var now = new DateTimeOffset(2026, 9, 21, 19, 5, 0, TimeSpan.Zero);

Run("AR-001", "rule creation is customer scoped and deterministic", () =>
{
    var service = NewService(out var store, out _, now);
    var rule = Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));

    Require(rule.CustomerId == CustomerA(), "rule owner mismatch");
    Require(rule.RuleId == RuleA(), "rule id mismatch");
    Require(rule.Revision == 1, "initial revision must be one");
    Require(rule.Name == "Portfolio floor", "name must be normalized");
    Require(rule.Channels.SequenceEqual([NotificationChannel.Email, NotificationChannel.InApp]), "channels must be deterministic");
    Require(store.Count == 1, "rule must persist once");
});

Run("AR-002", "cross-customer create fails closed", () =>
{
    var service = NewService(out _, out _, now);
    ExpectInvalidOperation(
        () => Await(service.CreateAsync(CustomerA(), CustomerB(), CreateCommand(RuleA()))),
        "CUSTOMER_ALERT_RULE_OWNERSHIP_MISMATCH");
});

Run("AR-003", "cross-customer reads fail closed", () =>
{
    var service = NewService(out _, out _, now);
    Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));
    ExpectInvalidOperation(
        () => Await(service.GetAsync(CustomerB(), CustomerA(), RuleA())),
        "CUSTOMER_ALERT_RULE_OWNERSHIP_MISMATCH");
});

Run("AR-004", "customer stores remain isolated", () =>
{
    var service = NewService(out var store, out _, now);
    Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));
    Await(service.CreateAsync(CustomerB(), CustomerB(), CreateCommand(RuleB())));
    var a = Await(service.ListAsync(CustomerA(), CustomerA()));
    var b = Await(service.ListAsync(CustomerB(), CustomerB()));

    Require(a.Count == 1 && a[0].RuleId == RuleA(), "customer A isolation failed");
    Require(b.Count == 1 && b[0].RuleId == RuleB(), "customer B isolation failed");
    Require(store.Count == 2, "store count mismatch");
});

Run("AR-005", "duplicate rule id within a customer is rejected", () =>
{
    var service = NewService(out _, out _, now);
    Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));
    ExpectInvalidOperation(
        () => Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA()))),
        "CUSTOMER_ALERT_RULE_ALREADY_EXISTS");
});

Run("AR-006", "unknown metric is rejected", () =>
{
    var service = NewService(out _, out _, now);
    ExpectThrows<ArgumentException>(() => Await(service.CreateAsync(
        CustomerA(),
        CustomerA(),
        CreateCommand(RuleA()) with { Metric = CustomerAlertMetric.Unknown })));
});

Run("AR-007", "undefined metric is rejected", () =>
{
    var service = NewService(out _, out _, now);
    ExpectThrows<ArgumentException>(() => Await(service.CreateAsync(
        CustomerA(),
        CustomerA(),
        CreateCommand(RuleA()) with { Metric = (CustomerAlertMetric)999 })));
});

Run("AR-008", "unknown comparison is rejected", () =>
{
    var service = NewService(out _, out _, now);
    ExpectThrows<ArgumentException>(() => Await(service.CreateAsync(
        CustomerA(),
        CustomerA(),
        CreateCommand(RuleA()) with { Comparison = CustomerAlertComparison.Unknown })));
});

Run("AR-009", "empty channel set is rejected", () =>
{
    var service = NewService(out _, out _, now);
    ExpectThrows<ArgumentException>(() => Await(service.CreateAsync(
        CustomerA(),
        CustomerA(),
        CreateCommand(RuleA()) with { Channels = Array.Empty<NotificationChannel>() })));
});

Run("AR-010", "duplicate channels are rejected", () =>
{
    var service = NewService(out _, out _, now);
    ExpectThrows<ArgumentException>(() => Await(service.CreateAsync(
        CustomerA(),
        CustomerA(),
        CreateCommand(RuleA()) with
        {
            Channels = [NotificationChannel.InApp, NotificationChannel.InApp],
        })));
});

Run("AR-011", "unsupported channel is rejected", () =>
{
    var service = NewService(out _, out _, now);
    ExpectThrows<ArgumentException>(() => Await(service.CreateAsync(
        CustomerA(),
        CustomerA(),
        CreateCommand(RuleA()) with { Channels = [(NotificationChannel)999] })));
});

Run("AR-012", "rule names are trimmed and bounded", () =>
{
    var service = NewService(out _, out _, now);
    var rule = Await(service.CreateAsync(
        CustomerA(),
        CustomerA(),
        CreateCommand(RuleA()) with { Name = "  Portfolio floor  " }));
    Require(rule.Name == "Portfolio floor", "rule name normalization failed");

    ExpectThrows<ArgumentOutOfRangeException>(() => Await(service.CreateAsync(
        CustomerA(),
        CustomerA(),
        CreateCommand(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa9")) with { Name = new string('x', 121) })));
});

Run("AR-013", "numeric range is fail closed", () =>
{
    var service = NewService(out _, out _, now);
    ExpectThrows<ArgumentOutOfRangeException>(() => Await(service.CreateAsync(
        CustomerA(),
        CustomerA(),
        CreateCommand(RuleA()) with { Threshold = 1_000_000_000_000_001m })));
});

Run("AR-014", "updates use optimistic revisions", () =>
{
    var service = NewService(out _, out _, now);
    var created = Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));
    var updated = Await(service.UpdateAsync(
        CustomerA(),
        CustomerA(),
        RuleA(),
        UpdateCommand(created.Revision) with { Threshold = 900m }));

    Require(updated.Revision == 2 && updated.Threshold == 900m, "update did not advance deterministically");
    ExpectInvalidOperation(
        () => Await(service.UpdateAsync(CustomerA(), CustomerA(), RuleA(), UpdateCommand(1))),
        "CUSTOMER_ALERT_RULE_REVISION_CONFLICT");
});

Run("AR-015", "disabled rules remain auditable and do not evaluate", () =>
{
    var service = NewService(out var store, out _, now);
    var created = Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));
    var disabled = Await(service.UpdateAsync(
        CustomerA(),
        CustomerA(),
        RuleA(),
        UpdateCommand(created.Revision) with { Enabled = false }));

    var result = Await(service.EvaluateAsync(CustomerA(), CustomerA(), [Observation(800m)]));
    Require(!disabled.Enabled && disabled.Revision == 2, "disabled state must be retained");
    Require(result.Candidates.Count == 0, "disabled rule must not produce candidate");
    Require(store.Count == 1, "disable must not delete audit state");
});

Run("AR-016", "comparison semantics are deterministic", () =>
{
    var rule = new CustomerAlertRule(
        RuleA(),
        CustomerA(),
        1,
        "threshold",
        CustomerAlertMetric.PaperPortfolioValue,
        CustomerAlertComparison.LessThanOrEqual,
        1000m,
        [NotificationChannel.InApp],
        true,
        now,
        now);

    Require(rule.Matches(Observation(1000m)), "equal threshold should match <=");
    Require(rule.Matches(Observation(999m)), "lower value should match <=");
    Require(!rule.Matches(Observation(1001m)), "higher value should not match <=");
});

Run("AR-017", "privacy-safe preference defaults allow only in-app", () =>
{
    var service = NewService(out _, out _, now);
    Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));

    var result = Await(service.EvaluateAsync(CustomerA(), CustomerA(), [Observation(900m)]));

    Require(result.Candidates.Count == 1, "matching default rule should produce one candidate");
    Require(result.Candidates[0].Channels.SequenceEqual([NotificationChannel.InApp]), "default preference boundary must suppress email");
});

Run("AR-018", "explicit preference opt-in enables configured external channel", () =>
{
    var service = NewService(out _, out var preferences, now);
    Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));
    Await(preferences.SetNotificationChannelAsync(CustomerA(), CustomerA(), 0, NotificationChannel.Email, true));

    var result = Await(service.EvaluateAsync(CustomerA(), CustomerA(), [Observation(900m)]));

    Require(result.Candidates.Single().Channels.SequenceEqual([NotificationChannel.Email, NotificationChannel.InApp]), "opted-in email must join in-app deterministically");
});

Run("AR-019", "missing observation does not produce a candidate", () =>
{
    var service = NewService(out _, out _, now);
    Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));

    var result = Await(service.EvaluateAsync(
        CustomerA(),
        CustomerA(),
        [new CustomerAlertObservation(CustomerAlertMetric.PaperCashBalance, 100m)]));

    Require(result.Candidates.Count == 0, "unobserved metric must not trigger");
});

Run("AR-020", "duplicate metric observations are rejected", () =>
{
    var service = NewService(out _, out _, now);
    ExpectThrows<ArgumentException>(() => Await(service.EvaluateAsync(
        CustomerA(),
        CustomerA(),
        [Observation(900m), Observation(800m)])));
});

Run("AR-021", "evaluation ordering is stable by rule id", () =>
{
    var service = NewService(out _, out _, now);
    Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleB())));
    Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));

    var result = Await(service.EvaluateAsync(CustomerA(), CustomerA(), [Observation(900m)]));

    Require(result.Candidates.Select(candidate => candidate.RuleId).SequenceEqual([RuleA(), RuleB()]), "candidate ordering must be stable");
});

Run("AR-022", "public alert payloads require no contact PII", () =>
{
    var publicTypes = new List<Type>
    {
        typeof(CustomerAlertRule),
        typeof(CustomerAlertObservation),
        typeof(CustomerAlertCandidate),
        typeof(CustomerAlertEvaluationResult),
    };
    var prohibited = new List<string>
    {
        "emailaddress", "phone", "mobile", "contact", "recipient", "address",
        "cnic", "passport", "iban", "bankaccount", "biometric",
    };

    foreach (var type in publicTypes)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var normalized = property.Name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            Require(!prohibited.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal)), $"restricted PII-like field {type.Name}.{property.Name}");
        }
    }
});

Run("AR-023", "authority is configuration-only and non-live", () =>
{
    var authority = CustomerAlertRuleAuthority.ConfigurationOnly;
    Require(authority.CustomerScoped, "authority must be customer scoped");
    Require(!authority.ContainsContactPii, "authority must declare no contact PII");
    Require(!authority.CanDispatchNotifications, "rules cannot dispatch notifications");
    Require(!authority.CanFetchLiveMarketData, "rules cannot fetch live market data");
    Require(!authority.CanPlaceOrders, "rules cannot place orders");
    Require(!authority.CanMoveMoney, "rules cannot move money");
});

Run("AR-024", "service surface exposes no dispatch trading or provider action", () =>
{
    var prohibitedMethodFragments = new List<string> { "Dispatch", "Send", "Trade", "PlaceOrder", "Withdraw", "Deposit" };
    var publicMethods = typeof(CustomerAlertRuleService)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Select(method => method.Name)
        .ToArray();

    Require(!publicMethods.Any(name => prohibitedMethodFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase))), "service exposes prohibited action method");

    var constructorParameters = typeof(CustomerAlertRuleService).GetConstructors().Single().GetParameters();
    Require(!constructorParameters.Any(parameter =>
        parameter.ParameterType.Name.Contains("Transport", StringComparison.OrdinalIgnoreCase)
        || parameter.ParameterType.Name.Contains("Broker", StringComparison.OrdinalIgnoreCase)
        || parameter.ParameterType.Name.Contains("Market", StringComparison.OrdinalIgnoreCase)
        || parameter.ParameterType.Name.Contains("Dispatcher", StringComparison.OrdinalIgnoreCase)), "service must not depend on live/action infrastructure");
});

Run("AR-025", "store contract has no destructive operation", () =>
{
    var destructive = typeof(ICustomerAlertRuleStore).GetMethods()
        .Where(method => method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
            || method.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase))
        .ToArray();
    Require(destructive.Length == 0, "store contract must retain alert rule evidence");
});

Run("AR-026", "pre-cancelled evaluation does not mutate state", () =>
{
    var service = NewService(out var store, out _, now);
    Await(service.CreateAsync(CustomerA(), CustomerA(), CreateCommand(RuleA())));
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    ExpectThrows<OperationCanceledException>(() => Await(service.EvaluateAsync(
        CustomerA(),
        CustomerA(),
        [Observation(900m)],
        cancellation.Token)));
    Require(store.Count == 1, "cancelled evaluation must not mutate rule state");
});

if (failures.Count != 0)
{
    Console.Error.WriteLine($"Customer alert rules verifier FAILED ({failures.Count}/26): {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine($"Customer alert rules verifier PASS ({passes}/26).");
Console.WriteLine("CUSTOMER_SCOPE: authenticated customer identity gates create/read/update/list/evaluate operations.");
Console.WriteLine("DETERMINISTIC_RULES: thresholds, comparisons, channel order, revisions and candidate ordering are stable.");
Console.WriteLine("PREFERENCE_BOUNDARY: configured channels are intersected with customer notification preferences before candidate output.");
Console.WriteLine("NO_CONTACT_PII: rule configuration/evaluation requires no email address, phone number or recipient contact data.");
Console.WriteLine("NO_DISPATCH_NO_LIVE_MARKET_NO_TRADING: evaluation returns non-live candidates only; no transport, broker, market fetch, money movement or order authority exists.");
return 0;

CreateCustomerAlertRuleCommand CreateCommand(Guid ruleId) =>
    new(
        ruleId,
        "Portfolio floor",
        CustomerAlertMetric.PaperPortfolioValue,
        CustomerAlertComparison.LessThan,
        1000m,
        [NotificationChannel.InApp, NotificationChannel.Email]);

UpdateCustomerAlertRuleCommand UpdateCommand(long expectedRevision) =>
    new(
        expectedRevision,
        "Portfolio floor",
        CustomerAlertMetric.PaperPortfolioValue,
        CustomerAlertComparison.LessThan,
        1000m,
        [NotificationChannel.InApp, NotificationChannel.Email],
        Enabled: true);

CustomerAlertObservation Observation(decimal value) =>
    new(CustomerAlertMetric.PaperPortfolioValue, value);

CustomerAlertRuleService NewService(
    out InMemoryCustomerAlertRuleStore ruleStore,
    out CustomerPreferenceService preferenceService,
    DateTimeOffset fixedNow)
{
    ruleStore = new InMemoryCustomerAlertRuleStore();
    var preferenceStore = new InMemoryCustomerPreferenceStore();
    preferenceService = new CustomerPreferenceService(preferenceStore, new FixedTimeProvider(fixedNow));
    return new CustomerAlertRuleService(ruleStore, preferenceService, new FixedTimeProvider(fixedNow));
}

static Guid CustomerA() => Guid.Parse("11111111-1111-1111-1111-111111111111");
static Guid CustomerB() => Guid.Parse("22222222-2222-2222-2222-222222222222");
static Guid RuleA() => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
static Guid RuleB() => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");

static T Await<T>(ValueTask<T> task) => task.AsTask().GetAwaiter().GetResult();

void Run(string id, string description, Action action)
{
    try
    {
        action();
        passes++;
        Console.WriteLine($"PASS {id}: {description}");
    }
    catch (Exception exception)
    {
        failures.Add(id);
        Console.Error.WriteLine($"FAIL {id}: {description}: {exception.GetType().Name}: {exception.Message}");
    }
}

static void ExpectInvalidOperation(Action action, string expectedMessage)
{
    try
    {
        action();
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

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"ASSERTION_FAILED:{message}");
    }
}

sealed class FixedTimeProvider(DateTimeOffset fixedNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => fixedNow;
}
