using System.Reflection;
using Klyvesta.Application.Preferences;
using Klyvesta.Domain.Notifications;
using Klyvesta.Domain.Preferences;
using Klyvesta.Infrastructure.Preferences;

var failures = new List<string>();
var passes = 0;
var fixedNow = new DateTimeOffset(2026, 9, 14, 18, 15, 0, TimeSpan.Zero);

Run("CP-001", "missing preferences return deterministic privacy-safe defaults", () =>
{
    var store = new InMemoryCustomerPreferenceStore();
    var service = new CustomerPreferenceService(store, new FixedTimeProvider(fixedNow));
    var customerId = CustomerA();

    var first = Await(service.GetAsync(customerId, customerId));
    var second = Await(service.GetAsync(customerId, customerId));

    AssertEqual(0L, first.Revision, "default revision");
    AssertEqual(DateTimeOffset.UnixEpoch, first.UpdatedAt, "default timestamp");
    AssertPreferenceStateEqual(first, second, "deterministic defaults");
    AssertEqual(0, store.Count, "read does not persist defaults");
});

Run("CP-002", "privacy-safe defaults enable only in-app delivery", () =>
{
    var snapshot = CustomerPreferenceSnapshot.PrivacySafeDefaults(CustomerA(), DateTimeOffset.UnixEpoch);

    Assert(snapshot.IsEnabled(NotificationChannel.InApp), "in-app default enabled");
    Assert(!snapshot.IsEnabled(NotificationChannel.Email), "email default disabled");
    Assert(!snapshot.IsEnabled(NotificationChannel.Sms), "sms default disabled");
    Assert(!snapshot.IsEnabled(NotificationChannel.Push), "push default disabled");
    Assert(!snapshot.IsEnabled(NotificationChannel.WhatsApp), "whatsapp default disabled");
});

Run("CP-003", "defaults explicitly cover every supported channel", () =>
{
    var snapshot = CustomerPreferenceSnapshot.PrivacySafeDefaults(CustomerA(), DateTimeOffset.UnixEpoch);
    var expected = new[]
    {
        NotificationChannel.Email,
        NotificationChannel.Sms,
        NotificationChannel.Push,
        NotificationChannel.InApp,
        NotificationChannel.WhatsApp,
    };

    AssertSequenceEqual(expected, snapshot.NotificationChannels.Select(item => item.Channel), "default channel coverage/order");
});

Run("CP-004", "explicit external-channel opt-in commits one revision", () =>
{
    var store = new InMemoryCustomerPreferenceStore();
    var service = new CustomerPreferenceService(store, new FixedTimeProvider(fixedNow));
    var customerId = CustomerA();

    var updated = Await(service.SetNotificationChannelAsync(
        customerId,
        customerId,
        expectedRevision: 0,
        NotificationChannel.Email,
        enabled: true));

    AssertEqual(1L, updated.Revision, "revision after opt-in");
    Assert(updated.IsEnabled(NotificationChannel.Email), "email opted in");
    Assert(updated.IsEnabled(NotificationChannel.InApp), "in-app preserved");
    AssertEqual(fixedNow, updated.UpdatedAt, "mutation timestamp");
    AssertEqual(1, store.Count, "persisted customer count");
});

Run("CP-005", "persisted preference is returned on subsequent read", () =>
{
    var store = new InMemoryCustomerPreferenceStore();
    var service = new CustomerPreferenceService(store, new FixedTimeProvider(fixedNow));
    var customerId = CustomerA();
    Await(service.SetNotificationChannelAsync(customerId, customerId, 0, NotificationChannel.Push, true));

    var read = Await(service.GetAsync(customerId, customerId));

    AssertEqual(1L, read.Revision, "persisted revision");
    Assert(read.IsEnabled(NotificationChannel.Push), "push remains enabled");
});

Run("CP-006", "setting the current value is idempotent and does not persist defaults", () =>
{
    var store = new InMemoryCustomerPreferenceStore();
    var service = new CustomerPreferenceService(store, new FixedTimeProvider(fixedNow));
    var customerId = CustomerA();

    var unchanged = Await(service.SetNotificationChannelAsync(
        customerId,
        customerId,
        expectedRevision: 0,
        NotificationChannel.Email,
        enabled: false));

    AssertEqual(0L, unchanged.Revision, "idempotent default revision");
    AssertEqual(0, store.Count, "idempotent default does not create state");
});

Run("CP-007", "setting the same persisted value does not advance revision", () =>
{
    var store = new InMemoryCustomerPreferenceStore();
    var service = new CustomerPreferenceService(store, new FixedTimeProvider(fixedNow));
    var customerId = CustomerA();
    var first = Await(service.SetNotificationChannelAsync(customerId, customerId, 0, NotificationChannel.Email, true));

    var second = Await(service.SetNotificationChannelAsync(customerId, customerId, first.Revision, NotificationChannel.Email, true));

    AssertEqual(first.Revision, second.Revision, "idempotent persisted revision");
    AssertPreferenceStateEqual(first, second, "idempotent persisted state");
});

Run("CP-008", "cross-customer read fails closed", () =>
{
    var service = NewService(out _, fixedNow);
    ExpectInvalidOperation(
        () => Await(service.GetAsync(CustomerA(), CustomerB())),
        "CUSTOMER_PREFERENCE_OWNERSHIP_MISMATCH");
});

Run("CP-009", "cross-customer mutation fails closed", () =>
{
    var service = NewService(out _, fixedNow);
    ExpectInvalidOperation(
        () => Await(service.SetNotificationChannelAsync(CustomerA(), CustomerB(), 0, NotificationChannel.InApp, false)),
        "CUSTOMER_PREFERENCE_OWNERSHIP_MISMATCH");
});

Run("CP-010", "missing authenticated customer id is rejected", () =>
{
    var service = NewService(out _, fixedNow);
    ExpectThrows<ArgumentException>(() => Await(service.GetAsync(Guid.Empty, CustomerA())));
});

Run("CP-011", "missing target customer id is rejected", () =>
{
    var service = NewService(out _, fixedNow);
    ExpectThrows<ArgumentException>(() => Await(service.GetAsync(CustomerA(), Guid.Empty)));
});

Run("CP-012", "unknown notification channel is rejected", () =>
{
    var service = NewService(out _, fixedNow);
    ExpectThrows<ArgumentException>(() => Await(service.SetNotificationChannelAsync(
        CustomerA(), CustomerA(), 0, NotificationChannel.Unknown, true)));
});

Run("CP-013", "undefined notification channel value is rejected", () =>
{
    var service = NewService(out _, fixedNow);
    ExpectThrows<ArgumentException>(() => Await(service.SetNotificationChannelAsync(
        CustomerA(), CustomerA(), 0, (NotificationChannel)999, true)));
});

Run("CP-014", "negative expected revision is rejected", () =>
{
    var service = NewService(out _, fixedNow);
    ExpectThrows<ArgumentOutOfRangeException>(() => Await(service.SetNotificationChannelAsync(
        CustomerA(), CustomerA(), -1, NotificationChannel.InApp, false)));
});

Run("CP-015", "stale service revision fails closed", () =>
{
    var service = NewService(out _, fixedNow);
    var customerId = CustomerA();
    Await(service.SetNotificationChannelAsync(customerId, customerId, 0, NotificationChannel.Email, true));

    ExpectInvalidOperation(
        () => Await(service.SetNotificationChannelAsync(customerId, customerId, 0, NotificationChannel.Sms, true)),
        "CUSTOMER_PREFERENCE_REVISION_CONFLICT");
});

Run("CP-016", "store rejects stale revision", () =>
{
    var store = new InMemoryCustomerPreferenceStore();
    var customerId = CustomerA();
    var first = CustomerPreferenceSnapshot.PrivacySafeDefaults(customerId, DateTimeOffset.UnixEpoch)
        .WithChannel(NotificationChannel.Email, true, fixedNow);
    Await(store.CommitAsync(first, 0));
    var stale = first.WithChannel(NotificationChannel.Sms, true, fixedNow.AddMinutes(1));

    ExpectInvalidOperation(
        () => Await(store.CommitAsync(stale, 0)),
        "CUSTOMER_PREFERENCE_REVISION_CONFLICT");
});

Run("CP-017", "store rejects invalid next revision", () =>
{
    var store = new InMemoryCustomerPreferenceStore();
    var candidate = new CustomerPreferenceSnapshot(
        CustomerA(),
        revision: 2,
        CustomerPreferenceSnapshot.PrivacySafeDefaults(CustomerA(), DateTimeOffset.UnixEpoch).NotificationChannels,
        fixedNow);

    ExpectInvalidOperation(
        () => Await(store.CommitAsync(candidate, 0)),
        "CUSTOMER_PREFERENCE_INVALID_NEXT_REVISION");
});

Run("CP-018", "customer state is isolated in the store", () =>
{
    var store = new InMemoryCustomerPreferenceStore();
    var service = new CustomerPreferenceService(store, new FixedTimeProvider(fixedNow));
    Await(service.SetNotificationChannelAsync(CustomerA(), CustomerA(), 0, NotificationChannel.Email, true));

    var other = Await(service.GetAsync(CustomerB(), CustomerB()));

    AssertEqual(0L, other.Revision, "other customer remains default");
    Assert(!other.IsEnabled(NotificationChannel.Email), "other customer did not inherit email opt-in");
    AssertEqual(1, store.Count, "only first customer persisted");
});

Run("CP-019", "channel output order remains deterministic after mutations", () =>
{
    var service = NewService(out _, fixedNow);
    var customerId = CustomerA();
    var first = Await(service.SetNotificationChannelAsync(customerId, customerId, 0, NotificationChannel.WhatsApp, true));
    var second = Await(service.SetNotificationChannelAsync(customerId, customerId, first.Revision, NotificationChannel.Email, true));
    var expected = new[]
    {
        NotificationChannel.Email,
        NotificationChannel.Sms,
        NotificationChannel.Push,
        NotificationChannel.InApp,
        NotificationChannel.WhatsApp,
    };

    AssertSequenceEqual(
        expected,
        second.NotificationChannels.Select(item => item.Channel),
        "mutated channel order");
});

Run("CP-020", "duplicate notification channel entries are rejected", () =>
{
    ExpectThrows<ArgumentException>(() =>
    {
        _ = new CustomerPreferenceSnapshot(
            CustomerA(),
            revision: 0,
            [
                new CustomerNotificationChannelPreference(NotificationChannel.InApp, true),
                new CustomerNotificationChannelPreference(NotificationChannel.InApp, false),
            ],
            fixedNow);
    });
});

Run("CP-021", "public preference schema requires no contact PII", () =>
{
    var preferenceTypes = new[]
    {
        typeof(CustomerPreferenceSnapshot),
        typeof(CustomerNotificationChannelPreference),
        typeof(CustomerPreferenceAuthority),
    };
    var prohibitedFragments = new List<string>
    {
        "emailaddress",
        "phone",
        "mobile",
        "contact",
        "recipient",
        "address",
        "cnic",
        "passport",
        "iban",
        "bankaccount",
        "biometric",
    };

    foreach (var type in preferenceTypes)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var normalized = property.Name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            Assert(
                !prohibitedFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal)),
                $"restricted PII-like preference field {type.Name}.{property.Name}");
        }
    }

    var stringParameters = typeof(CustomerPreferenceService)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .SelectMany(method => method.GetParameters())
        .Where(parameter => parameter.ParameterType == typeof(string))
        .ToArray();
    AssertEqual(0, stringParameters.Length, "service requires no contact string input");
});

Run("CP-022", "preference authority cannot dispatch, trade or change security policy", () =>
{
    var authority = CustomerPreferenceAuthority.PreferenceOnly;

    Assert(authority.CustomerScoped, "authority remains customer scoped");
    Assert(!authority.ContainsContactPii, "contains no contact PII");
    Assert(!authority.CanDispatchNotifications, "cannot dispatch notifications");
    Assert(!authority.CanChangeSecurityPolicy, "cannot change security policy");
    Assert(!authority.CanPlaceOrders, "cannot place orders");
});

Run("CP-023", "mutation preserves unrelated channel choices", () =>
{
    var service = NewService(out _, fixedNow);
    var customerId = CustomerA();
    var first = Await(service.SetNotificationChannelAsync(customerId, customerId, 0, NotificationChannel.Email, true));
    var second = Await(service.SetNotificationChannelAsync(customerId, customerId, first.Revision, NotificationChannel.Push, true));
    var third = Await(service.SetNotificationChannelAsync(customerId, customerId, second.Revision, NotificationChannel.Email, false));

    Assert(!third.IsEnabled(NotificationChannel.Email), "email disabled");
    Assert(third.IsEnabled(NotificationChannel.Push), "push choice preserved");
    Assert(third.IsEnabled(NotificationChannel.InApp), "in-app choice preserved");
});

Run("CP-024", "store honors cancellation without mutating state", () =>
{
    var store = new InMemoryCustomerPreferenceStore();
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    ExpectThrows<OperationCanceledException>(() => Await(store.FindAsync(CustomerA(), cancellation.Token)));
    AssertEqual(0, store.Count, "cancelled store remains unchanged");
});

if (failures.Count != 0)
{
    Console.Error.WriteLine($"Customer preferences verifier FAILED ({failures.Count}/24): {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine($"Customer preferences verifier PASS ({passes}/24).");
return 0;

void Run(string id, string name, Action action)
{
    try
    {
        action();
        passes++;
        Console.WriteLine($"PASS {id}: {name}");
    }
    catch (Exception exception)
    {
        failures.Add(id);
        Console.Error.WriteLine($"FAIL {id}: {name}: {exception.Message}");
    }
}

static Guid CustomerA() => Guid.Parse("11111111-1111-1111-1111-111111111111");

static Guid CustomerB() => Guid.Parse("22222222-2222-2222-2222-222222222222");

static CustomerPreferenceService NewService(out InMemoryCustomerPreferenceStore store, DateTimeOffset now)
{
    store = new InMemoryCustomerPreferenceStore();
    return new CustomerPreferenceService(store, new FixedTimeProvider(now));
}

static T Await<T>(ValueTask<T> task) => task.AsTask().GetAwaiter().GetResult();

static void AssertPreferenceStateEqual(
    CustomerPreferenceSnapshot expected,
    CustomerPreferenceSnapshot actual,
    string message)
{
    AssertEqual(expected.CustomerId, actual.CustomerId, $"{message} customer");
    AssertEqual(expected.Revision, actual.Revision, $"{message} revision");
    AssertEqual(expected.UpdatedAt, actual.UpdatedAt, $"{message} timestamp");
    AssertSequenceEqual(expected.NotificationChannels, actual.NotificationChannels, $"{message} channels");
    AssertEqual(expected.Authority, actual.Authority, $"{message} authority");
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

static void AssertSequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException($"ASSERTION_FAILED:{message}");
    }
}

sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
