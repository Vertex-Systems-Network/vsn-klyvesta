namespace Klyvesta.Infrastructure.Broker.PyPsx;

public sealed record PyPsxReconciliationSnapshot(
    string AccountId,
    decimal ReportedCash,
    decimal ProjectedCash,
    decimal ReportedEquity,
    decimal ProjectedEquity,
    IReadOnlyDictionary<string, decimal> ReportedPositions,
    IReadOnlyDictionary<string, decimal> ProjectedPositions);

public sealed record PyPsxReconciliationDifference(
    string Field,
    decimal Expected,
    decimal Actual);

public static class PyPsxReconciliation
{
    public static IReadOnlyList<PyPsxReconciliationDifference> Compare(
        PyPsxReconciliationSnapshot snapshot,
        decimal tolerance = 0.01m)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        var differences = new List<PyPsxReconciliationDifference>();

        AddIfOutsideTolerance(differences, "cash", snapshot.ProjectedCash, snapshot.ReportedCash, tolerance);
        AddIfOutsideTolerance(differences, "equity", snapshot.ProjectedEquity, snapshot.ReportedEquity, tolerance);

        var symbols = snapshot.ReportedPositions.Keys
            .Concat(snapshot.ProjectedPositions.Keys)
            .Distinct(StringComparer.Ordinal);

        foreach (var symbol in symbols)
        {
            var reported = snapshot.ReportedPositions.GetValueOrDefault(symbol);
            var projected = snapshot.ProjectedPositions.GetValueOrDefault(symbol);
            AddIfOutsideTolerance(differences, $"position:{symbol}", projected, reported, tolerance);
        }

        return differences;
    }

    private static void AddIfOutsideTolerance(
        List<PyPsxReconciliationDifference> differences,
        string field,
        decimal expected,
        decimal actual,
        decimal tolerance)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            differences.Add(new(field, expected, actual));
        }
    }
}
