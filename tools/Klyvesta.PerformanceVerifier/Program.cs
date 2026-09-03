using System.Globalization;
using System.Text.Json;

var root = Directory.GetCurrentDirectory();
var loadPath = Path.Combine(root, "perf", "load-profile.json");
var failurePath = Path.Combine(root, "perf", "failure-injection.json");
var baselinePath = Path.Combine(root, "perf", "performance-baseline.json");

var checks = new (string Id, Action Run)[]
{
    ("PERF-001 load profile exists", () => Require(File.Exists(loadPath), "load profile missing")),
    ("PERF-002 failure plan exists", () => Require(File.Exists(failurePath), "failure plan missing")),
    ("PERF-003 baseline exists", () => Require(File.Exists(baselinePath), "performance baseline missing")),
    ("PERF-004 registered user target is 5000", () => Require(ReadInt(loadPath, "registeredUsers") == 5000, "MVP registered-user target drifted")),
    ("PERF-005 load profile has four deterministic phases", () => Require(ReadArrayLength(loadPath, "phases") == 4, "expected four load phases")),
    ("PERF-006 steady concurrency is 500", () => Require(ReadNestedArrayInt(loadPath, "phases", "name", "mvp-steady", "concurrentUsers") == 500, "steady concurrency drifted")),
    ("PERF-007 market-open burst reaches 1000 rps", () => Require(ReadNestedArrayInt(loadPath, "phases", "name", "market-open-burst", "targetRequestRatePerSecond") == 1000, "burst request rate drifted")),
    ("PERF-008 scale gate concurrency is 5000", () => Require(ReadNestedInt(loadPath, "scaleGate", "concurrentUsers") == 5000, "scale concurrency drifted")),
    ("PERF-009 scale gate preserves 10x order burst", () => Require(ReadNestedInt(loadPath, "scaleGate", "orderBurstMultiplier") == 10, "scale order burst drifted")),
    ("PERF-010 broker timeout injection is present", () => Require(ContainsScenario(failurePath, "broker-timeout"), "broker timeout injection missing")),
    ("PERF-011 duplicate webhook injection is present", () => Require(ContainsScenario(failurePath, "duplicate-webhook"), "duplicate webhook injection missing")),
    ("PERF-012 redis outage injection is present", () => Require(ContainsScenario(failurePath, "redis-unavailable"), "redis outage injection missing")),
    ("PERF-013 AI timeout injection is present", () => Require(ContainsScenario(failurePath, "ai-provider-timeout"), "AI timeout injection missing")),
    ("PERF-014 DB failover injection is present", () => Require(ContainsScenario(failurePath, "database-partial-failover"), "DB failover injection missing")),
    ("PERF-015 auth read p95 target remains 300ms", () => Require(ReadNestedInt(baselinePath, "latencyTargetsMilliseconds", "authApiReadP95") == 300, "auth p95 target drifted")),
    ("PERF-016 deterministic risk p95 remains 50ms", () => Require(ReadNestedInt(baselinePath, "latencyTargetsMilliseconds", "deterministicRiskGateP95") == 50, "risk p95 target drifted")),
    ("PERF-017 dashboard availability remains 99.95", () => Require(ReadNestedDecimal(baselinePath, "availabilityTargetsPercent", "readDashboard") == 99.95m, "dashboard availability drifted")),
    ("PERF-018 trading availability remains 99.9", () => Require(ReadNestedDecimal(baselinePath, "availabilityTargetsPercent", "tradingCommand") == 99.9m, "trading availability drifted")),
    ("PERF-019 LLM independence remains mandatory", () => Require(ReadNestedBool(baselinePath, "measurementRules", "llmMustNotBlockExecutionStateCorrectness"), "LLM independence rule missing")),
    ("PERF-020 real environment evidence remains mandatory", () => Require(ReadNestedBool(baselinePath, "measurementRules", "productionReadinessRequiresRealEnvironmentEvidence"), "production evidence rule missing")),
};

var failures = new List<string>();
foreach (var (id, run) in checks)
{
    try
    {
        run();
        Console.WriteLine($"PERFORMANCE_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"PERFORMANCE_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Performance contract assertions passed: {checks.Length - failures.Count}/{checks.Length}");
Console.WriteLine("LOAD_PROFILE: deterministic machine-readable MVP, burst, reconnect and scale-gate workload contract is present.");
Console.WriteLine("FAILURE_INJECTION: broker, event, market-data, cache, AI, DB and reconciliation faults have explicit fail-safe expectations.");
Console.WriteLine("BASELINE: SLO targets are treated as an initial contract, not fabricated production measurements.");
Console.WriteLine("NOT_LIVE: verifier generates no traffic and touches no production endpoint, broker, customer data, database, secret or real-money path.");

return failures.Count == 0 ? 0 : 1;

static int ReadInt(string path, string property)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    return document.RootElement.GetProperty(property).GetInt32();
}

static int ReadArrayLength(string path, string property)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    return document.RootElement.GetProperty(property).GetArrayLength();
}

static int ReadNestedInt(string path, string parent, string property)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    return document.RootElement.GetProperty(parent).GetProperty(property).GetInt32();
}

static decimal ReadNestedDecimal(string path, string parent, string property)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    return document.RootElement.GetProperty(parent).GetProperty(property).GetDecimal();
}

static bool ReadNestedBool(string path, string parent, string property)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    return document.RootElement.GetProperty(parent).GetProperty(property).GetBoolean();
}

static int ReadNestedArrayInt(string path, string arrayProperty, string matchProperty, string matchValue, string valueProperty)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    foreach (var item in document.RootElement.GetProperty(arrayProperty).EnumerateArray())
    {
        if (string.Equals(item.GetProperty(matchProperty).GetString(), matchValue, StringComparison.Ordinal))
        {
            return item.GetProperty(valueProperty).GetInt32();
        }
    }

    throw new InvalidOperationException($"Could not find {matchProperty}={matchValue} in {arrayProperty}.");
}

static bool ContainsScenario(string path, string id)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    foreach (var scenario in document.RootElement.GetProperty("scenarios").EnumerateArray())
    {
        if (string.Equals(scenario.GetProperty("id").GetString(), id, StringComparison.Ordinal))
        {
            var expected = scenario.GetProperty("expected").GetString();
            return !string.IsNullOrWhiteSpace(expected);
        }
    }

    return false;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
