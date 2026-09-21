using Klyvesta.Domain.Portfolios;
using Klyvesta.Domain.Risk;
using Klyvesta.Domain.Scenarios;

namespace Klyvesta.Application.Scenarios;

public sealed record CustomerScenarioRequest(
    Guid AuthenticatedCustomerId,
    Guid CustomerId,
    string AccountReference,
    DateTimeOffset EvaluatedAt,
    TimeSpan MaximumRiskContextAge,
    PortfolioProjectionSnapshot Portfolio,
    RiskPortfolioSnapshot RiskContext,
    CustomerScenarioShock Shock);

public interface ICustomerScenarioEngine
{
    CustomerScenarioResult Evaluate(CustomerScenarioRequest request);
}

public sealed class DeterministicCustomerScenarioEngine : ICustomerScenarioEngine
{
    private const decimal MinimumShockFraction = -1m;
    private const decimal MaximumShockFraction = 1m;

    public CustomerScenarioResult Evaluate(CustomerScenarioRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCustomerScope(request.AuthenticatedCustomerId, request.CustomerId);
        var accountReference = RequireText(request.AccountReference, "Account reference");

        if (request.EvaluatedAt == default)
        {
            throw new ArgumentException("Scenario evaluation time is required.", nameof(request));
        }

        if (request.MaximumRiskContextAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Maximum risk-context age must be positive.");
        }

        ArgumentNullException.ThrowIfNull(request.Portfolio);
        ArgumentNullException.ThrowIfNull(request.RiskContext);
        ArgumentNullException.ThrowIfNull(request.Shock);

        EnsureAccountScope(request.Portfolio.AccountReference, accountReference, "CUSTOMER_SCENARIO_PORTFOLIO_ACCOUNT_SCOPE_MISMATCH");
        EnsureAccountScope(request.RiskContext.AccountReference, accountReference, "CUSTOMER_SCENARIO_RISK_ACCOUNT_SCOPE_MISMATCH");
        ValidatePortfolio(request.Portfolio);
        ValidateRiskContext(request.RiskContext, request.EvaluatedAt, request.MaximumRiskContextAge);

        if (request.Portfolio.Cash != request.RiskContext.Cash)
        {
            throw new InvalidOperationException("CUSTOMER_SCENARIO_CASH_CONTEXT_MISMATCH");
        }

        var sectorShocks = NormalizeShock(request.Shock);
        var portfolioByInstrument = request.Portfolio.Positions.ToDictionary(
            static position => NormalizeInstrument(position.InstrumentReference),
            static position => position,
            StringComparer.Ordinal);
        var riskByInstrument = request.RiskContext.Positions.ToDictionary(
            static position => NormalizeInstrument(position.InstrumentReference),
            static position => position,
            StringComparer.Ordinal);

        if (portfolioByInstrument.Count != riskByInstrument.Count)
        {
            throw new InvalidOperationException("CUSTOMER_SCENARIO_POSITION_SET_MISMATCH");
        }

        var results = new List<CustomerScenarioPositionResult>(portfolioByInstrument.Count);
        foreach (var pair in portfolioByInstrument.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!riskByInstrument.TryGetValue(pair.Key, out var riskPosition))
            {
                throw new InvalidOperationException("CUSTOMER_SCENARIO_POSITION_SET_MISMATCH");
            }

            var portfolioPosition = pair.Value;
            if (portfolioPosition.Quantity != riskPosition.Quantity)
            {
                throw new InvalidOperationException("CUSTOMER_SCENARIO_POSITION_QUANTITY_MISMATCH");
            }

            var sectorReference = RequireText(riskPosition.SectorReference, "Risk sector reference");
            var shockFraction = sectorShocks.TryGetValue(sectorReference, out var sectorShock)
                ? sectorShock
                : request.Shock.MarketShockFraction;
            var shockedPrice = riskPosition.MarketPrice * (1m + shockFraction);
            var currentValue = riskPosition.Quantity * riskPosition.MarketPrice;
            var shockedValue = riskPosition.Quantity * shockedPrice;

            results.Add(new CustomerScenarioPositionResult(
                pair.Key,
                sectorReference,
                riskPosition.Quantity,
                riskPosition.MarketPrice,
                shockFraction,
                shockedPrice,
                currentValue,
                shockedValue,
                shockedValue - currentValue));
        }

        foreach (var instrument in riskByInstrument.Keys)
        {
            if (!portfolioByInstrument.ContainsKey(instrument))
            {
                throw new InvalidOperationException("CUSTOMER_SCENARIO_POSITION_SET_MISMATCH");
            }
        }

        var currentInvestedValue = results.Sum(static position => position.CurrentValue);
        var shockedInvestedValue = results.Sum(static position => position.ShockedValue);
        var currentTotalValue = request.Portfolio.Cash + currentInvestedValue;
        var shockedTotalValue = request.Portfolio.Cash + shockedInvestedValue;
        var absoluteImpact = shockedTotalValue - currentTotalValue;
        var impactFraction = currentTotalValue == 0m
            ? 0m
            : absoluteImpact / currentTotalValue;

        return new CustomerScenarioResult(
            request.CustomerId,
            accountReference,
            request.EvaluatedAt,
            request.MaximumRiskContextAge,
            request.Portfolio.Cash,
            currentInvestedValue,
            currentTotalValue,
            shockedInvestedValue,
            shockedTotalValue,
            absoluteImpact,
            impactFraction,
            CustomerScenarioRiskBasis.ExistingRiskPortfolioSnapshot,
            results.AsReadOnly(),
            CustomerScenarioAuthority.Informational);
    }

    private static void EnsureCustomerScope(Guid authenticatedCustomerId, Guid customerId)
    {
        if (authenticatedCustomerId == Guid.Empty)
        {
            throw new ArgumentException("Authenticated customer ID is required.", nameof(authenticatedCustomerId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        if (authenticatedCustomerId != customerId)
        {
            throw new InvalidOperationException("CUSTOMER_SCENARIO_SCOPE_MISMATCH");
        }
    }

    private static void EnsureAccountScope(string sourceAccountReference, string requestedAccountReference, string errorCode)
    {
        if (!StringComparer.Ordinal.Equals(RequireText(sourceAccountReference, "Source account reference"), requestedAccountReference))
        {
            throw new InvalidOperationException(errorCode);
        }
    }

    private static void ValidatePortfolio(PortfolioProjectionSnapshot portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio.Positions);

        if (portfolio.Cash < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(portfolio), "Paper portfolio cash cannot be negative.");
        }

        if (portfolio.LastSequence < -1 ||
            portfolio.UniqueSourceEventCount < 0 ||
            portfolio.UniqueExecutionCount < 0 ||
            portfolio.UniqueExecutionCount > portfolio.UniqueSourceEventCount)
        {
            throw new InvalidOperationException("CUSTOMER_SCENARIO_PORTFOLIO_COUNTERS_INVALID");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var position in portfolio.Positions)
        {
            var instrument = NormalizeInstrument(position.InstrumentReference);
            if (!seen.Add(instrument))
            {
                throw new InvalidOperationException("CUSTOMER_SCENARIO_PORTFOLIO_DUPLICATE_INSTRUMENT");
            }

            if (position.Quantity <= 0m || position.AverageCost <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(portfolio), "Paper portfolio position quantity and average cost must be positive.");
            }
        }
    }

    private static void ValidateRiskContext(
        RiskPortfolioSnapshot riskContext,
        DateTimeOffset evaluatedAt,
        TimeSpan maximumRiskContextAge)
    {
        ArgumentNullException.ThrowIfNull(riskContext.Positions);

        if (riskContext.Cash < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(riskContext), "Risk-context cash cannot be negative.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var position in riskContext.Positions)
        {
            var instrument = NormalizeInstrument(position.InstrumentReference);
            if (!seen.Add(instrument))
            {
                throw new InvalidOperationException("CUSTOMER_SCENARIO_RISK_DUPLICATE_INSTRUMENT");
            }

            _ = RequireText(position.SectorReference, "Risk sector reference");
            if (position.Quantity <= 0m || position.MarketPrice <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(riskContext), "Risk position quantity and market price must be positive.");
            }

            if (position.MarketObservedAt > evaluatedAt ||
                evaluatedAt - position.MarketObservedAt > maximumRiskContextAge)
            {
                throw new InvalidOperationException("CUSTOMER_SCENARIO_RISK_CONTEXT_STALE_OR_FUTURE");
            }
        }
    }

    private static Dictionary<string, decimal> NormalizeShock(CustomerScenarioShock shock)
    {
        ValidateShockFraction(shock.MarketShockFraction, "market");
        ArgumentNullException.ThrowIfNull(shock.SectorShockFractions);

        var normalized = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var pair in shock.SectorShockFractions)
        {
            var sector = RequireText(pair.Key, "Sector shock key");
            ValidateShockFraction(pair.Value, sector);
            if (!normalized.TryAdd(sector, pair.Value))
            {
                throw new InvalidOperationException("CUSTOMER_SCENARIO_DUPLICATE_SECTOR_SHOCK");
            }
        }

        return normalized;
    }

    private static void ValidateShockFraction(decimal shockFraction, string label)
    {
        if (shockFraction < MinimumShockFraction || shockFraction > MaximumShockFraction)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shockFraction),
                $"{label} shock fraction must be between -1 and 1.");
        }
    }

    private static string NormalizeInstrument(string instrumentReference) =>
        RequireText(instrumentReference, "Instrument reference");

    private static string RequireText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} is required.");
        }

        return value.Trim();
    }
}
