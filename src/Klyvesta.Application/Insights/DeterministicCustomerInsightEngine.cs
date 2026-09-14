using Klyvesta.Domain.Insights;
using Klyvesta.Domain.Risk;

namespace Klyvesta.Application.Insights;

public sealed class CustomerInsightThresholds
{
    public CustomerInsightThresholds(
        decimal drawdownAlertFraction,
        decimal nearLimitFraction,
        decimal goalPaceToleranceFraction)
    {
        DrawdownAlertFraction = RequireFraction(drawdownAlertFraction, nameof(drawdownAlertFraction), allowZero: false);
        NearLimitFraction = RequireFraction(nearLimitFraction, nameof(nearLimitFraction), allowZero: false);
        GoalPaceToleranceFraction = RequireFraction(goalPaceToleranceFraction, nameof(goalPaceToleranceFraction), allowZero: true);
    }

    public decimal DrawdownAlertFraction { get; }

    public decimal NearLimitFraction { get; }

    public decimal GoalPaceToleranceFraction { get; }

    private static decimal RequireFraction(decimal value, string parameterName, bool allowZero)
    {
        if (value > 1m || value < 0m || (!allowZero && value == 0m))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Fraction must be within the supported 0..1 range.");
        }

        return value;
    }
}

public sealed class CustomerGoalProgressInput
{
    public CustomerGoalProgressInput(
        decimal currentAmount,
        decimal targetAmount,
        DateOnly startedOn,
        DateOnly targetDate)
    {
        if (currentAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(currentAmount), "Goal current amount cannot be negative.");
        }

        if (targetAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(targetAmount), "Goal target amount must be positive.");
        }

        if (targetDate <= startedOn)
        {
            throw new ArgumentException("Goal target date must be after its start date.", nameof(targetDate));
        }

        CurrentAmount = currentAmount;
        TargetAmount = targetAmount;
        StartedOn = startedOn;
        TargetDate = targetDate;
    }

    public decimal CurrentAmount { get; }

    public decimal TargetAmount { get; }

    public DateOnly StartedOn { get; }

    public DateOnly TargetDate { get; }
}

public sealed record CustomerInsightRequest(
    RiskPortfolioSnapshot Portfolio,
    PaperRiskPolicy RiskPolicy,
    RiskActivityWindow Activity,
    CustomerInsightThresholds Thresholds,
    decimal PeakPortfolioValue,
    CustomerGoalProgressInput? Goal,
    DateTimeOffset AsOf);

public interface ICustomerInsightEngine
{
    CustomerInsightReport Generate(CustomerInsightRequest request);
}

public sealed class DeterministicCustomerInsightEngine : ICustomerInsightEngine
{
    public CustomerInsightReport Generate(CustomerInsightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Portfolio);
        ArgumentNullException.ThrowIfNull(request.RiskPolicy);
        ArgumentNullException.ThrowIfNull(request.Activity);
        ArgumentNullException.ThrowIfNull(request.Thresholds);

        ValidatePolicy(request.RiskPolicy);
        ValidateActivity(request.Activity, request.AsOf);
        var valuedPositions = ValidateAndValuePositions(request.Portfolio, request.AsOf);
        if (request.Portfolio.Cash < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Portfolio cash cannot be negative for customer insights.");
        }

        var portfolioValue = request.Portfolio.Cash + valuedPositions.Sum(position => position.Value);
        if (portfolioValue <= 0m)
        {
            throw new InvalidOperationException("CUSTOMER_INSIGHT_PORTFOLIO_VALUE_REQUIRED");
        }

        if (request.PeakPortfolioValue < portfolioValue)
        {
            throw new InvalidOperationException("CUSTOMER_INSIGHT_PEAK_BELOW_CURRENT_VALUE");
        }

        var evidenceFresh = valuedPositions.All(position =>
            request.AsOf - position.ObservedAt <= request.RiskPolicy.MaxMarketDataAge);
        var insights = new List<CustomerInsight>();

        if (!evidenceFresh)
        {
            insights.Add(CreateInsight(
                "MARKET_EVIDENCE_STALE",
                CustomerInsightSeverity.Warning,
                "Valuation evidence is stale",
                "Valuation-based concentration, exposure and drawdown insights were suppressed because at least one position price is older than the configured market-data age boundary.",
                new CustomerInsightMetric("max_market_data_age_seconds", (decimal)request.RiskPolicy.MaxMarketDataAge.TotalSeconds)));
        }
        else
        {
            AddConcentrationInsights(insights, valuedPositions, portfolioValue, request.RiskPolicy);
            AddExposureInsight(insights, valuedPositions, portfolioValue, request.RiskPolicy, request.Thresholds);
            AddDrawdownInsight(insights, portfolioValue, request.PeakPortfolioValue, request.Thresholds);
        }

        if (request.Goal is not null)
        {
            AddGoalInsight(insights, request.Goal, request.AsOf, request.Thresholds);
        }

        AddActivityInsight(insights, request.Activity, request.RiskPolicy, request.Thresholds);

        return new CustomerInsightReport(
            request.Portfolio.AccountReference,
            request.AsOf,
            portfolioValue,
            evidenceFresh,
            insights.OrderBy(insight => insight.Code, StringComparer.Ordinal).ToArray(),
            CustomerInsightAuthority.Informational);
    }

    private static ValuedPosition[] ValidateAndValuePositions(RiskPortfolioSnapshot portfolio, DateTimeOffset asOf)
    {
        if (string.IsNullOrWhiteSpace(portfolio.AccountReference))
        {
            throw new ArgumentException("Portfolio account reference is required.", nameof(portfolio));
        }

        ArgumentNullException.ThrowIfNull(portfolio.Positions);
        var duplicates = portfolio.Positions
            .GroupBy(position => position.InstrumentReference, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicates is not null)
        {
            throw new InvalidOperationException("CUSTOMER_INSIGHT_DUPLICATE_INSTRUMENT");
        }

        return portfolio.Positions.Select(position =>
        {
            if (string.IsNullOrWhiteSpace(position.InstrumentReference) || string.IsNullOrWhiteSpace(position.SectorReference))
            {
                throw new ArgumentException("Position instrument and sector references are required.", nameof(portfolio));
            }

            if (position.Quantity < 0m || position.MarketPrice <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(portfolio), "Position quantity cannot be negative and market price must be positive.");
            }

            if (position.MarketObservedAt > asOf)
            {
                throw new InvalidOperationException("CUSTOMER_INSIGHT_FUTURE_MARKET_EVIDENCE");
            }

            return new ValuedPosition(
                position.InstrumentReference,
                position.SectorReference,
                position.Quantity * position.MarketPrice,
                position.MarketObservedAt);
        }).ToArray();
    }

    private static void ValidatePolicy(PaperRiskPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Version))
        {
            throw new ArgumentException("Risk policy version is required.", nameof(policy));
        }

        if (policy.MaxMarketDataAge <= TimeSpan.Zero ||
            policy.MaxSinglePositionFraction <= 0m ||
            policy.MaxSectorFraction <= 0m ||
            policy.MaxGrossExposure <= 0m ||
            policy.MaxOrdersPerWindow <= 0 ||
            policy.MaxTurnoverPerWindow <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Risk envelope limits used by customer insights must be positive.");
        }
    }

    private static void ValidateActivity(RiskActivityWindow activity, DateTimeOffset asOf)
    {
        if (activity.StartedAt > asOf)
        {
            throw new InvalidOperationException("CUSTOMER_INSIGHT_FUTURE_ACTIVITY_WINDOW");
        }

        if (activity.AcceptedOrderCount < 0 || activity.AcceptedTurnoverNotional < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(activity), "Activity counts and turnover cannot be negative.");
        }
    }

    private static void AddConcentrationInsights(
        List<CustomerInsight> insights,
        IReadOnlyList<ValuedPosition> positions,
        decimal portfolioValue,
        PaperRiskPolicy policy)
    {
        if (positions.Count == 0)
        {
            return;
        }

        var largestPosition = positions
            .Select(position => new { Position = position, Fraction = position.Value / portfolioValue })
            .OrderByDescending(item => item.Fraction)
            .ThenBy(item => item.Position.InstrumentReference, StringComparer.Ordinal)
            .First();
        if (largestPosition.Fraction > policy.MaxSinglePositionFraction)
        {
            insights.Add(CreateInsight(
                "POSITION_CONCENTRATION",
                CustomerInsightSeverity.Warning,
                "Single-position concentration exceeds the configured envelope",
                FormattableString.Invariant($"{largestPosition.Position.InstrumentReference} represents {largestPosition.Fraction:P1} of the portfolio versus a configured {policy.MaxSinglePositionFraction:P1} single-position envelope."),
                new CustomerInsightMetric("position_fraction", largestPosition.Fraction),
                new CustomerInsightMetric("configured_limit", policy.MaxSinglePositionFraction)));
        }

        var largestSector = positions
            .GroupBy(position => position.SectorReference, StringComparer.Ordinal)
            .Select(group => new { Sector = group.Key, Fraction = group.Sum(position => position.Value) / portfolioValue })
            .OrderByDescending(item => item.Fraction)
            .ThenBy(item => item.Sector, StringComparer.Ordinal)
            .First();
        if (largestSector.Fraction > policy.MaxSectorFraction)
        {
            insights.Add(CreateInsight(
                "SECTOR_CONCENTRATION",
                CustomerInsightSeverity.Warning,
                "Sector concentration exceeds the configured envelope",
                FormattableString.Invariant($"{largestSector.Sector} represents {largestSector.Fraction:P1} of the portfolio versus a configured {policy.MaxSectorFraction:P1} sector envelope."),
                new CustomerInsightMetric("sector_fraction", largestSector.Fraction),
                new CustomerInsightMetric("configured_limit", policy.MaxSectorFraction)));
        }
    }

    private static void AddExposureInsight(
        List<CustomerInsight> insights,
        IReadOnlyList<ValuedPosition> positions,
        decimal portfolioValue,
        PaperRiskPolicy policy,
        CustomerInsightThresholds thresholds)
    {
        var grossExposureFraction = positions.Sum(position => Math.Abs(position.Value)) / portfolioValue;
        if (grossExposureFraction > policy.MaxGrossExposure)
        {
            insights.Add(CreateInsight(
                "GROSS_EXPOSURE_OVER_LIMIT",
                CustomerInsightSeverity.Warning,
                "Gross exposure is above the configured risk envelope",
                FormattableString.Invariant($"Gross exposure is {grossExposureFraction:P1} of portfolio value versus a configured {policy.MaxGrossExposure:P1} envelope."),
                new CustomerInsightMetric("gross_exposure_fraction", grossExposureFraction),
                new CustomerInsightMetric("configured_limit", policy.MaxGrossExposure)));
            return;
        }

        if (grossExposureFraction >= policy.MaxGrossExposure * thresholds.NearLimitFraction)
        {
            insights.Add(CreateInsight(
                "GROSS_EXPOSURE_NEAR_LIMIT",
                CustomerInsightSeverity.Attention,
                "Gross exposure is near the configured risk envelope",
                FormattableString.Invariant($"Gross exposure is {grossExposureFraction:P1} of portfolio value and has reached the configured near-limit observation threshold."),
                new CustomerInsightMetric("gross_exposure_fraction", grossExposureFraction),
                new CustomerInsightMetric("configured_limit", policy.MaxGrossExposure)));
        }
    }

    private static void AddDrawdownInsight(
        List<CustomerInsight> insights,
        decimal portfolioValue,
        decimal peakPortfolioValue,
        CustomerInsightThresholds thresholds)
    {
        if (peakPortfolioValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(peakPortfolioValue), "Peak portfolio value must be positive.");
        }

        var drawdownFraction = (peakPortfolioValue - portfolioValue) / peakPortfolioValue;
        if (drawdownFraction >= thresholds.DrawdownAlertFraction)
        {
            insights.Add(CreateInsight(
                "DRAWDOWN_ALERT",
                CustomerInsightSeverity.Attention,
                "Portfolio drawdown crossed the observation threshold",
                FormattableString.Invariant($"Current portfolio value is {drawdownFraction:P1} below the supplied peak value; the configured observation threshold is {thresholds.DrawdownAlertFraction:P1}."),
                new CustomerInsightMetric("drawdown_fraction", drawdownFraction),
                new CustomerInsightMetric("observation_threshold", thresholds.DrawdownAlertFraction)));
        }
    }

    private static void AddGoalInsight(
        List<CustomerInsight> insights,
        CustomerGoalProgressInput goal,
        DateTimeOffset asOf,
        CustomerInsightThresholds thresholds)
    {
        var asOfDate = DateOnly.FromDateTime(asOf.UtcDateTime);
        if (asOfDate < goal.StartedOn)
        {
            throw new InvalidOperationException("CUSTOMER_INSIGHT_GOAL_NOT_STARTED");
        }

        var totalDays = goal.TargetDate.DayNumber - goal.StartedOn.DayNumber;
        var elapsedDays = Math.Clamp(asOfDate.DayNumber - goal.StartedOn.DayNumber, 0, totalDays);
        var timeFraction = elapsedDays / (decimal)totalDays;
        var progressFraction = goal.CurrentAmount / goal.TargetAmount;
        var behindPace = progressFraction + thresholds.GoalPaceToleranceFraction < timeFraction;

        insights.Add(CreateInsight(
            behindPace ? "GOAL_PROGRESS_BEHIND_PACE" : "GOAL_PROGRESS_ON_PACE",
            behindPace ? CustomerInsightSeverity.Attention : CustomerInsightSeverity.Information,
            behindPace ? "Goal progress is behind elapsed-time pace" : "Goal progress is at or ahead of elapsed-time pace",
            FormattableString.Invariant($"Goal funding progress is {progressFraction:P1} while {timeFraction:P1} of the configured goal period has elapsed. This is an informational comparison, not a return forecast or recommendation."),
            new CustomerInsightMetric("goal_progress_fraction", progressFraction),
            new CustomerInsightMetric("goal_time_fraction", timeFraction)));
    }

    private static void AddActivityInsight(
        List<CustomerInsight> insights,
        RiskActivityWindow activity,
        PaperRiskPolicy policy,
        CustomerInsightThresholds thresholds)
    {
        var orderFraction = activity.AcceptedOrderCount / (decimal)policy.MaxOrdersPerWindow;
        var turnoverFraction = activity.AcceptedTurnoverNotional / policy.MaxTurnoverPerWindow;
        var intensityFraction = Math.Max(orderFraction, turnoverFraction);
        if (intensityFraction < thresholds.NearLimitFraction)
        {
            return;
        }

        insights.Add(CreateInsight(
            "ACTIVITY_INTENSITY_ELEVATED",
            CustomerInsightSeverity.Attention,
            "Recent activity is near the configured paper-risk activity envelope",
            FormattableString.Invariant($"Recent order or turnover activity has reached {intensityFraction:P1} of its configured paper-risk envelope. This signal does not place, block or recommend any trade."),
            new CustomerInsightMetric("order_count_fraction", orderFraction),
            new CustomerInsightMetric("turnover_fraction", turnoverFraction)));
    }

    private static CustomerInsight CreateInsight(
        string code,
        CustomerInsightSeverity severity,
        string summary,
        string explanation,
        params CustomerInsightMetric[] metrics) =>
        new(code, severity, summary, explanation, metrics);

    private sealed record ValuedPosition(
        string InstrumentReference,
        string SectorReference,
        decimal Value,
        DateTimeOffset ObservedAt);
}
