using System.Text.RegularExpressions;

var root = FindRepositoryRoot();
var failures = new List<string>();
var passes = 0;

Check("PLAT-001", "dotnet verifier auto-discovery exists", () =>
{
    var workflow = Read(".github/workflows/dotnet-foundation.yml");
    Require(workflow.Contains("find tools -type f -name '*Verifier.csproj'", StringComparison.Ordinal),
        "dotnet-foundation must discover verifier projects by convention");
    Require(Regex.Count(workflow, "find tools -type f -name '\\*Verifier\\.csproj'") >= 4,
        "restore/format/build/run stages must all use verifier discovery rather than a hard-coded project list");
});

Check("PLAT-002", "all external workflow actions are immutable-SHA pinned", () =>
{
    foreach (var workflowPath in WorkflowFiles())
    {
        foreach (var rawLine in File.ReadLines(workflowPath))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("uses:", StringComparison.Ordinal) && !line.StartsWith("- uses:", StringComparison.Ordinal))
            {
                continue;
            }

            var value = line[(line.IndexOf("uses:", StringComparison.Ordinal) + 5)..].Trim();
            var comment = value.IndexOf('#');
            if (comment >= 0)
            {
                value = value[..comment].Trim();
            }

            if (value.StartsWith("./", StringComparison.Ordinal) || value.StartsWith("docker://", StringComparison.Ordinal))
            {
                continue;
            }

            var at = value.LastIndexOf('@');
            Require(at > 0 && at < value.Length - 1,
                $"{Relative(workflowPath)} contains an external action without a ref: {value}");
            var reference = value[(at + 1)..];
            Require(Regex.IsMatch(reference, "^[0-9a-fA-F]{40}$|^[0-9a-fA-F]{64}$"),
                $"{Relative(workflowPath)} action must use an immutable commit SHA, found: {value}");
        }
    }
});

Check("PLAT-003", "checkout credentials are not persisted", () =>
{
    foreach (var workflowPath in WorkflowFiles())
    {
        var text = File.ReadAllText(workflowPath);
        if (!text.Contains("actions/checkout@", StringComparison.Ordinal))
        {
            continue;
        }

        Require(text.Contains("persist-credentials: false", StringComparison.Ordinal),
            $"{Relative(workflowPath)} uses checkout but does not explicitly disable persisted credentials");
    }
});

Check("PLAT-004", "workflow permissions are explicit and broad write-all is forbidden", () =>
{
    foreach (var workflowPath in WorkflowFiles())
    {
        var text = File.ReadAllText(workflowPath);
        Require(text.Contains("permissions:", StringComparison.Ordinal),
            $"{Relative(workflowPath)} must declare permissions explicitly");
        Require(!text.Contains("permissions: write-all", StringComparison.OrdinalIgnoreCase),
            $"{Relative(workflowPath)} must not grant write-all permissions");
    }
});

Check("PLAT-005", "pull_request_target is not used", () =>
{
    foreach (var workflowPath in WorkflowFiles())
    {
        var text = File.ReadAllText(workflowPath);
        Require(!Regex.IsMatch(text, "(?m)^\\s*pull_request_target\\s*:"),
            $"{Relative(workflowPath)} must not introduce pull_request_target without a dedicated reviewed threat model");
    }
});

Check("PLAT-006", "shared engineering paths remain centrally owned", () =>
{
    var manifest = Read(".ai/agent-orchestration.yaml");
    Require(manifest.Contains("README.md", StringComparison.Ordinal), "shared-path ownership is missing README.md");
    Require(manifest.Contains(".github/workflows/**", StringComparison.Ordinal), "shared-path ownership is missing .github/workflows/**");
    Require(manifest.Contains("Directory.Build.props", StringComparison.Ordinal), "shared-path ownership is missing Directory.Build.props");
    Require(manifest.Contains("Directory.Packages.props", StringComparison.Ordinal), "shared-path ownership is missing Directory.Packages.props");
    Require(manifest.Contains(".ai/agent-orchestration.yaml", StringComparison.Ordinal), "shared-path ownership is missing .ai/agent-orchestration.yaml");
    Require(manifest.Contains(".ai/parallel-branch-registry.yaml", StringComparison.Ordinal), "shared-path ownership is missing .ai/parallel-branch-registry.yaml");
    Require(manifest.Contains(".ai/integration-baseline.yaml", StringComparison.Ordinal), "shared-path ownership is missing .ai/integration-baseline.yaml");
    Require(manifest.Contains("contracts/**", StringComparison.Ordinal), "shared-path ownership is missing contracts/**");
});

Check("PLAT-007", "platform lane is explicitly registered", () =>
{
    var manifest = Read(".ai/agent-orchestration.yaml");
    var registry = Read(".ai/parallel-branch-registry.yaml");
    Require(manifest.Contains("platform-ci:", StringComparison.Ordinal), "platform-ci module is missing from orchestration");
    Require(manifest.Contains("canonical_branch: parallel/platform-ci", StringComparison.Ordinal), "platform-ci canonical branch is missing");
    Require(registry.Contains("module: platform-ci", StringComparison.Ordinal), "platform-ci registry entry is missing");
    Require(registry.Contains("branch: parallel/platform-ci", StringComparison.Ordinal), "platform-ci registry branch is missing");
    Require(registry.Contains("agent_name: ChatGPT-Platform-01", StringComparison.Ordinal), "platform-ci worker assignment is missing");
});

Check("PLAT-008", "platform work item forbids product-source and migration writes", () =>
{
    var workItem = Read(".ai/work-items/platform-ci/M-AGENT-09-platform-ci.yaml");
    Require(workItem.Contains("src/**", StringComparison.Ordinal), "platform work item must explicitly forbid src/**");
    Require(workItem.Contains("**/Migrations/**", StringComparison.Ordinal), "platform work item must explicitly forbid **/Migrations/**");
    Require(workItem.Contains("**/*ModelSnapshot.cs", StringComparison.Ordinal), "platform work item must explicitly forbid **/*ModelSnapshot.cs");
    Require(workItem.Contains("contracts/**", StringComparison.Ordinal), "platform work item must explicitly forbid contracts/**");
});

Check("PLAT-009", "parallel capacity policy is not weakened", () =>
{
    var manifest = Read(".ai/agent-orchestration.yaml");
    Require(manifest.Contains("current_recommended_min: 6", StringComparison.Ordinal),
        "current recommended minimum must remain 6");
    Require(manifest.Contains("current_recommended_max: 10", StringComparison.Ordinal),
        "current recommended maximum must remain 10");
});

Check("PLAT-010", "no-slot and refresh safety signals are preserved", () =>
{
    var manifest = Read(".ai/agent-orchestration.yaml");
    Require(manifest.Contains("Go Home Come Back Next Time", StringComparison.Ordinal), "no-slot message drifted");
    Require(manifest.Contains("New changes have been merged — please merge these changes into your branch first, then resume your own work.", StringComparison.Ordinal),
        "refresh safety alert drifted");
});

Check("PLAT-011", "P1-22 through P1-25 customer-lane closeouts are durable", () =>
{
    var registry = Read(".ai/parallel-branch-registry.yaml");
    var dashboard = Read(".ai/work-items/customer-dashboard/P1-22-customer-dashboard.yaml");
    var alerts = Read(".ai/work-items/customer-alert-rules/P1-23-customer-alert-rules.yaml");
    var scenarios = Read(".ai/work-items/customer-scenarios/P1-24-customer-scenarios.yaml");
    var securityCenter = Read(".ai/work-items/customer-security-center/P1-25-customer-security-center.yaml");

    Require(registry.Contains("module: customer-dashboard, branch: parallel/customer-dashboard, agent_slot: agent-customer-dashboard, status: INTEGRATED", StringComparison.Ordinal), "customer-dashboard must be integrated in the registry");
    Require(registry.Contains("integrated_sha: ef1f9912cc2928771dee0d104a29dec0563c9323", StringComparison.Ordinal), "customer-dashboard integration SHA must match the accepted merge");
    Require(dashboard.Contains("start_status: COMPLETE", StringComparison.Ordinal) && dashboard.Contains("status: INTEGRATED", StringComparison.Ordinal), "P1-22 work item must be durably complete and integrated");
    Require(dashboard.Contains("integration_baseline_sha: ef1f9912cc2928771dee0d104a29dec0563c9323", StringComparison.Ordinal), "P1-22 integration baseline must match accepted staging");

    Require(registry.Contains("module: customer-alert-rules, branch: parallel/customer-alert-rules, agent_slot: agent-customer-alert-rules, status: INTEGRATED", StringComparison.Ordinal), "customer-alert-rules must be integrated in the registry");
    Require(registry.Contains("integrated_sha: d134b2839ff1af3b6867f11b3304779a29b14b0b", StringComparison.Ordinal), "customer-alert-rules integration SHA must match PR #131 accepted merge");
    Require(alerts.Contains("start_status: COMPLETE", StringComparison.Ordinal) && alerts.Contains("status: INTEGRATED", StringComparison.Ordinal), "P1-23 work item must be durably complete and integrated");
    Require(alerts.Contains("integration_baseline_sha: d134b2839ff1af3b6867f11b3304779a29b14b0b", StringComparison.Ordinal), "P1-23 integration baseline must match accepted staging");
    Require(alerts.Contains("- exact-head-ci", StringComparison.Ordinal), "P1-23 exact-head CI acceptance evidence must be satisfied");
    Require(alerts.Contains("production_authority: false", StringComparison.Ordinal), "P1-23 must remain non-production after integration");

    Require(registry.Contains("module: customer-scenarios, branch: parallel/customer-scenarios, agent_slot: agent-customer-scenarios, status: INTEGRATED", StringComparison.Ordinal), "customer-scenarios must be integrated in the registry");
    Require(registry.Contains("integrated_sha: 73bc0b9deaf2fbf1bb44d0c1ee17d1f97d59cc14", StringComparison.Ordinal), "customer-scenarios integration SHA must match PR #135 accepted merge");
    Require(scenarios.Contains("start_status: COMPLETE", StringComparison.Ordinal) && scenarios.Contains("status: INTEGRATED", StringComparison.Ordinal), "P1-24 work item must be durably complete and integrated");
    Require(scenarios.Contains("integration_baseline_sha: 73bc0b9deaf2fbf1bb44d0c1ee17d1f97d59cc14", StringComparison.Ordinal), "P1-24 integration baseline must match PR #135 accepted staging");
    Require(scenarios.Contains("- exact-head-ci", StringComparison.Ordinal), "P1-24 exact-head CI acceptance evidence must be satisfied");
    Require(scenarios.Contains("production_authority: false", StringComparison.Ordinal), "P1-24 must remain non-production after integration");

    Require(registry.Contains("module: customer-security-center, branch: parallel/customer-security-center, agent_slot: agent-customer-security-center, status: INTEGRATED", StringComparison.Ordinal), "customer-security-center must be integrated in the registry");
    Require(registry.Contains("integrated_sha: 02c532252d3e67dc904c37254c0f13ff2d9c1a9f", StringComparison.Ordinal), "customer-security-center integration SHA must match PR #138 accepted merge");
    Require(securityCenter.Contains("start_status: COMPLETE", StringComparison.Ordinal) && securityCenter.Contains("status: INTEGRATED", StringComparison.Ordinal), "P1-25 work item must be durably complete and integrated");
    Require(securityCenter.Contains("integration_baseline_sha: 02c532252d3e67dc904c37254c0f13ff2d9c1a9f", StringComparison.Ordinal), "P1-25 integration baseline must match accepted staging");
    Require(securityCenter.Contains("- exact-head-ci", StringComparison.Ordinal), "P1-25 exact-head CI acceptance evidence must be satisfied");
    Require(securityCenter.Contains("production_authority: false", StringComparison.Ordinal), "P1-25 must remain non-production after integration");
});

Check("PLAT-012", "README module delivery table tracks canonical lifecycle state", () =>
{
    var readme = Read("README.md");
    var integrationBaseline = Read(".ai/integration-baseline.yaml");
    var branchMatches = Regex.Matches(
        integrationBaseline,
        @"(?m)^baseline_branch:\s*([A-Za-z0-9._/-]+)\s*$");
    var parentMatches = Regex.Matches(
        integrationBaseline,
        @"(?m)^verified_parent_baseline_sha:\s*([0-9a-f]{40})\s*$");

    Require(branchMatches.Count == 1,
        "integration baseline must expose exactly one canonical baseline_branch");
    Require(parentMatches.Count == 1,
        "integration baseline must expose exactly one verified_parent_baseline_sha");
    Require(integrationBaseline.Contains("current_head_authority: runtime_branch_resolution", StringComparison.Ordinal),
        "current integration head authority must be runtime branch resolution");
    Require(integrationBaseline.Contains("persisted_current_head_required: false", StringComparison.Ordinal),
        "immutable control-plane state must not require its own current commit SHA");

    var acceptedBranch = branchMatches[0].Groups[1].Value;
    var verifiedParentSha = parentMatches[0].Groups[1].Value;

    Require(readme.Contains("## Module delivery table", StringComparison.Ordinal), "README module delivery table is missing");
    Require(readme.Contains($"Accepted integration branch: `{acceptedBranch}`", StringComparison.Ordinal),
        $"README accepted integration branch is stale; expected {acceptedBranch}");
    Require(readme.Contains($"Verified parent baseline: `{verifiedParentSha}`", StringComparison.Ordinal),
        $"README verified parent baseline is stale; expected {verifiedParentSha}");
    Require(readme.Contains("21 of 24 canonical lanes are accepted/integrated", StringComparison.Ordinal),
        "README accepted-lane summary is stale");
    Require(Regex.IsMatch(readme, "(?m)^\\| Customer Dashboard \\|.*Integrated —"),
        "README customer-dashboard row must be integrated");
    Require(Regex.IsMatch(readme, "(?m)^\\| Customer Alert Rules \\|.*Integrated —"),
        "README customer-alert-rules row must be integrated");
    Require(Regex.IsMatch(readme, "(?m)^\\| Customer Scenarios \\|.*Integrated —"),
        "README customer-scenarios row must be integrated");
    Require(Regex.IsMatch(readme, "(?m)^\\| Customer Security Center \\|.*Integrated —"),
        "README customer-security-center row must be integrated");
    Require(Regex.IsMatch(readme, "(?m)^\\| Customer Risk Center \\|.*Active —"),
        "README customer-risk-center row must reflect the active P1-26 assignment");
    Require(Regex.IsMatch(readme, "(?m)^\\| Database Integration \\|.*Ready —"),
        "README database-integration row must remain ready");
    Require(Regex.IsMatch(readme, "(?m)^\\| Security Acceptance \\|.*Blocked —"),
        "README security-acceptance row must remain blocked");
});

Check("PLAT-013", "P1-26 active assignment is durable and bounded", () =>
{
    var manifest = Read(".ai/agent-orchestration.yaml");
    var registry = Read(".ai/parallel-branch-registry.yaml");
    var workItem = Read(".ai/work-items/customer-risk-center/P1-26-customer-risk-center.yaml");

    Require(manifest.Contains("customer-risk-center:", StringComparison.Ordinal), "customer-risk-center orchestration module is missing");
    Require(manifest.Contains("canonical_branch: parallel/customer-risk-center", StringComparison.Ordinal), "customer-risk-center canonical branch is missing");
    Require(registry.Contains("module: customer-risk-center, branch: parallel/customer-risk-center, agent_slot: agent-customer-risk-center, status: ACTIVE, occupancy: OCCUPIED, agent_name: ChatGPT-CustomerRiskCenter-01", StringComparison.Ordinal),
        "customer-risk-center must be ACTIVE/OCCUPIED by the assigned agent");
    Require(workItem.Contains("assigned_agent: ChatGPT-CustomerRiskCenter-01", StringComparison.Ordinal), "P1-26 assigned agent is missing");
    Require(workItem.Contains("start_status: ASSIGNED", StringComparison.Ordinal) && workItem.Contains("status: ACTIVE", StringComparison.Ordinal),
        "P1-26 work item must be durably assigned and active");
    var acceptedBaselineMatch = Regex.Match(
        workItem,
        "(?m)^accepted_baseline_sha:\\s*([0-9a-f]{40})\\s*$");
    Require(acceptedBaselineMatch.Success,
        "P1-26 accepted baseline must be one full immutable SHA");
    var acceptedBaseline = acceptedBaselineMatch.Groups[1].Value;
    Require(workItem.Contains($"base_sha: {acceptedBaseline}", StringComparison.Ordinal),
        "P1-26 base SHA must match its accepted baseline consumption record");
    Require(workItem.Contains($"consumed_runtime_baseline_sha: {acceptedBaseline}", StringComparison.Ordinal),
        "P1-26 runtime-consumed baseline must match its accepted baseline record");
    Require(workItem.Contains("runtime_integration_baseline_ancestor_verified: true", StringComparison.Ordinal),
        "P1-26 runtime integration baseline ancestry must be verified");
    Require(workItem.Contains("customer_data_dependency_ancestor_verified: true", StringComparison.Ordinal), "P1-26 customer-data ancestry evidence is missing");
    Require(workItem.Contains("portfolio_dependency_ancestor_verified: true", StringComparison.Ordinal), "P1-26 portfolio ancestry evidence is missing");
    Require(workItem.Contains("risk_dependency_ancestor_verified: true", StringComparison.Ordinal), "P1-26 risk ancestry evidence is missing");
    Require(workItem.Contains("production_authority: false", StringComparison.Ordinal), "P1-26 must not claim production authority");
    Require(workItem.Contains("no-advice-no-trading-no-provider-authority", StringComparison.Ordinal),
        "P1-26 must retain the no-advice/no-trading/provider boundary");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Platform CI verification FAILED ({failures.Count}):");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($" - {failure}");
    }

    return 1;
}

Console.WriteLine($"Platform CI verification PASS ({passes}/13 checks). No product source, API composition, migration, or workflow mutation was required.");
return 0;

void Check(string id, string description, Action assertion)
{
    try
    {
        assertion();
        passes++;
        Console.WriteLine($"PASS {id}: {description}");
    }
    catch (InvalidOperationException exception)
    {
        failures.Add($"{id}: {exception.Message}");
    }
}

void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

string Read(string relativePath) => File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

IEnumerable<string> WorkflowFiles()
{
    var directory = Path.Combine(root, ".github", "workflows");
    return Directory.EnumerateFiles(directory, "*.yml", SearchOption.TopDirectoryOnly)
        .Concat(Directory.EnumerateFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly))
        .OrderBy(static path => path, StringComparer.Ordinal);
}

string Relative(string absolutePath) => Path.GetRelativePath(root, absolutePath).Replace(Path.DirectorySeparatorChar, '/');

string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, ".ai", "agent-orchestration.yaml")) &&
            Directory.Exists(Path.Combine(directory.FullName, ".github", "workflows")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Unable to locate repository root from verifier execution directory.");
}
