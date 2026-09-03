using System.Text.RegularExpressions;

var root = FindRepositoryRoot();
var failures = new List<string>();
var passes = 0;

Check("PLAT-001", "dotnet verifier auto-discovery exists", () =>
{
    var workflow = Read(".github/workflows/dotnet-foundation.yml");
    Require(workflow.Contains("find tools -type f -name '*Verifier.csproj'", StringComparison.Ordinal),
        "dotnet-foundation must discover verifier projects by convention");
    Require(Regex.Matches(workflow, "find tools -type f -name '\\*Verifier\\.csproj'").Count >= 4,
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
    foreach (var required in new[]
             {
                 ".github/workflows/**",
                 "Directory.Build.props",
                 "Directory.Packages.props",
                 ".ai/agent-orchestration.yaml",
                 ".ai/parallel-branch-registry.yaml",
                 ".ai/integration-baseline.yaml",
                 "contracts/**"
             })
    {
        Require(manifest.Contains(required, StringComparison.Ordinal),
            $"shared-path ownership is missing {required}");
    }
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
    foreach (var forbidden in new[] { "src/**", "**/Migrations/**", "**/*ModelSnapshot.cs", "contracts/**" })
    {
        Require(workItem.Contains(forbidden, StringComparison.Ordinal),
            $"platform work item must explicitly forbid {forbidden}");
    }
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

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Platform CI verification FAILED ({failures.Count}):");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($" - {failure}");
    }

    return 1;
}

Console.WriteLine($"Platform CI verification PASS ({passes}/10 checks). No product source, API composition, migration, or workflow mutation was required.");
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
