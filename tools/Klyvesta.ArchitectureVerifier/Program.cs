using System.Xml.Linq;

var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);

var rules = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
{
    ["src/Klyvesta.Domain/Klyvesta.Domain.csproj"] = [],
    ["src/Klyvesta.Application/Klyvesta.Application.csproj"] = ["Klyvesta.Domain"],
    ["src/Klyvesta.Infrastructure/Klyvesta.Infrastructure.csproj"] = ["Klyvesta.Domain", "Klyvesta.Application"],
    ["src/Klyvesta.Api/Klyvesta.Api.csproj"] = ["Klyvesta.Application", "Klyvesta.Infrastructure"],
};

var failures = new List<string>();

foreach (var (relativePath, allowedReferences) in rules)
{
    var projectPath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    var document = XDocument.Load(projectPath, LoadOptions.SetLineInfo);
    var references = document
        .Descendants("ProjectReference")
        .Select(element => element.Attribute("Include")?.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => Path.GetFileNameWithoutExtension(value!))
        .ToHashSet(StringComparer.Ordinal);

    var forbidden = references.Except(allowedReferences, StringComparer.Ordinal).Order().ToArray();
    var missing = allowedReferences.Except(references, StringComparer.Ordinal).Order().ToArray();

    if (forbidden.Length > 0)
    {
        failures.Add($"{relativePath} has forbidden project references: {string.Join(", ", forbidden)}");
    }

    if (missing.Length > 0)
    {
        failures.Add($"{relativePath} is missing expected project references: {string.Join(", ", missing)}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("Architecture verification failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }

    return 1;
}

Console.WriteLine("Architecture project-reference boundaries verified.");
return 0;

static string FindRepositoryRoot(string startDirectory)
{
    var current = new DirectoryInfo(startDirectory);

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Repository root could not be located.");
}
