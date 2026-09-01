using Klyvesta.Domain.Risk;

namespace Klyvesta.Application.Risk;

public sealed class DeterministicRiskGovernor
{
    public RiskDecision Evaluate(PaperRiskPolicy policy, RiskEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(request);
        ValidatePolicy(policy);

        if (string.IsNullOrWhiteSpace(request.AccountReference) ||
            string.IsNullOrWhiteSpace(request.InstrumentReference) ||
            request.Quantity <= 0m)
        {
            return Deny(policy, request, "RISK_INVALID_ORDER_INPUT");
        }

        if (request.Instrument is null)
        {
            return Deny(policy, request, "RISK_INSTRUMENT_METADATA_MISSING");
        }

        if (request.MarketEvidence is null)
        {
            return Hold(policy, request, "RISK_MARKET_EVIDENCE_MISSING");
        }

        if (request.Portfolio is null)
        {
            return Hold(policy, request, "RISK_PORTFOLIO_CONTEXT_MISSING");
        }

        if (request.Activity is null)
        {
            return Hold(policy, request, "RISK_ACTIVITY_CONTEXT_MISSING");
        }

        var instrument = request.Instrument;
        var market = request.MarketEvidence;
        var portfolio = request.Portfolio;
        var activity = request.Activity;
        var reasons = new List<string>();

        if (policy.KillSwitchEnabled)
        {
            reasons.Add("RISK_KILL_SWITCH_ACTIVE");
        }

        if (!StringComparer.Ordinal.Equals(instrument.InstrumentReference, request.InstrumentReference) ||
            !StringComparer.Ordinal.Equals(market.InstrumentReference, request.InstrumentReference))
        {
            reasons.Add("RISK_INSTRUMENT_CONTEXT_MISMATCH");
        }

        if (!policy.AllowedInstrumentReferences.Contains(request.InstrumentReference))
        {
            reasons.Add("RISK_INSTRUMENT_NOT_ALLOWED");
        }

        if (!instrument.IsEligible)
        {
            reasons.Add("RISK_INSTRUMENT_NOT_ELIGIBLE");
        }

        if (string.IsNullOrWhiteSpace(instrument.SectorReference))
        {
            reasons.Add("RISK_SECTOR_METADATA_MISSING");
        }

        if (instrument.AssetClass is RiskAssetClass.Derivative or RiskAssetClass.Unknown)
        {
            reasons.Add(instrument.AssetClass == RiskAssetClass.Derivative
                ? "RISK_DERIVATIVE_PROHIBITED"
                : "RISK_ASSET_CLASS_UNKNOWN");
        }

        if (instrument.IsLeveragedProduct || request.RequestedLeverageMultiple > 1m)
        {
            reasons.Add("RISK_LEVERAGE_PROHIBITED");
        }

        if (instrument.RequiresMargin || request.UsesMargin)
        {
            reasons.Add("RISK_MARGIN_PROHIBITED");
        }

        if (request.RequestedLeverageMultiple <= 0m)
        {
            reasons.Add("RISK_INVALID_LEVERAGE_INPUT");
        }

        if (market.LastPrice <= 0m || request.ExpectedPrice <= 0m)
        {
            reasons.Add("RISK_INVALID_MARKET_PRICE");
        }

        if (market.ObservedAt > request.EvaluatedAt || request.EvaluatedAt - market.ObservedAt > policy.MaxMarketDataAge)
        {
            reasons.Add("RISK_MARKET_DATA_STALE");
        }

        if (market.DailyTradedValue < policy.MinimumDailyTradedValue)
        {
            reasons.Add("RISK_LIQUIDITY_BELOW_MINIMUM");
        }

        if (!StringComparer.Ordinal.Equals(portfolio.AccountReference, request.AccountReference))
        {
            reasons.Add("RISK_PORTFOLIO_ACCOUNT_MISMATCH");
        }

        var duplicatePosition = portfolio.Positions
            .GroupBy(position => position.InstrumentReference, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePosition is not null)
        {
            reasons.Add("RISK_PORTFOLIO_DUPLICATE_INSTRUMENT");
        }

        foreach (var position in portfolio.Positions)
        {
            if (string.IsNullOrWhiteSpace(position.InstrumentReference) ||
                string.IsNullOrWhiteSpace(position.SectorReference) ||
                position.Quantity < 0m ||
                position.MarketPrice <= 0m ||
                position.MarketObservedAt > request.EvaluatedAt ||
                request.EvaluatedAt - position.MarketObservedAt > policy.MaxMarketDataAge)
            {
                reasons.Add("RISK_PORTFOLIO_MARKET_CONTEXT_INVALID_OR_STALE");
                break;
            }
        }

        if (activity.StartedAt > request.EvaluatedAt)
        {
            reasons.Add("RISK_ACTIVITY_WINDOW_TIME_INVALID");
        }

        if (reasons.Count != 0 &&
            (market.LastPrice <= 0m || request.ExpectedPrice <= 0m || duplicatePosition is not null))
        {
            return Deny(policy, request, reasons);
        }

        var orderNotional = request.Quantity * request.ExpectedPrice;
        if (orderNotional > policy.MaxOrderNotional)
        {
            reasons.Add("RISK_ORDER_NOTIONAL_LIMIT_EXCEEDED");
        }

        var currentTarget = portfolio.Positions
            .FirstOrDefault(position => StringComparer.Ordinal.Equals(position.InstrumentReference, request.InstrumentReference));
        if (currentTarget is not null &&
            !StringComparer.Ordinal.Equals(currentTarget.SectorReference, instrument.SectorReference))
        {
            reasons.Add("RISK_TARGET_SECTOR_CONTEXT_CONFLICT");
        }

        var currentQuantity = currentTarget?.Quantity ?? 0m;
        var projectedTargetQuantity = request.Side == RiskTradeSide.Buy
            ? currentQuantity + request.Quantity
            : currentQuantity - request.Quantity;
        if (projectedTargetQuantity < 0m)
        {
            reasons.Add("RISK_SHORT_SELLING_PROHIBITED");
        }

        var projectedCash = request.Side == RiskTradeSide.Buy
            ? portfolio.Cash - orderNotional
            : portfolio.Cash + orderNotional;
        if (projectedCash < 0m)
        {
            reasons.Add("RISK_PROJECTED_CASH_NEGATIVE");
        }

        var projectedValues = new Dictionary<string, ProjectedValue>(StringComparer.Ordinal);
        foreach (var position in portfolio.Positions)
        {
            if (StringComparer.Ordinal.Equals(position.InstrumentReference, request.InstrumentReference))
            {
                continue;
            }

            projectedValues[position.InstrumentReference] = new ProjectedValue(
                position.SectorReference,
                position.Quantity * position.MarketPrice);
        }

        if (projectedTargetQuantity > 0m)
        {
            projectedValues[request.InstrumentReference] = new ProjectedValue(
                instrument.SectorReference,
                projectedTargetQuantity * request.ExpectedPrice);
        }

        var grossExposure = projectedValues.Values.Sum(value => Math.Abs(value.MarketValue));
        if (grossExposure > policy.MaxGrossExposure)
        {
            reasons.Add("RISK_GROSS_EXPOSURE_LIMIT_EXCEEDED");
        }

        var nav = projectedCash + projectedValues.Values.Sum(value => value.MarketValue);
        decimal? positionFraction = null;
        decimal? sectorFraction = null;
        if (nav <= 0m)
        {
            reasons.Add("RISK_PROJECTED_NAV_NON_POSITIVE");
        }
        else
        {
            var targetValue = projectedValues.TryGetValue(request.InstrumentReference, out var projectedTarget)
                ? projectedTarget.MarketValue
                : 0m;
            positionFraction = targetValue / nav;
            if (positionFraction > policy.MaxSinglePositionFraction)
            {
                reasons.Add("RISK_POSITION_CONCENTRATION_LIMIT_EXCEEDED");
            }

            var targetSectorValue = projectedValues.Values
                .Where(value => StringComparer.Ordinal.Equals(value.SectorReference, instrument.SectorReference))
                .Sum(value => value.MarketValue);
            sectorFraction = targetSectorValue / nav;
            if (sectorFraction > policy.MaxSectorFraction)
            {
                reasons.Add("RISK_SECTOR_CONCENTRATION_LIMIT_EXCEEDED");
            }
        }

        var activityIsCurrent = activity.StartedAt <= request.EvaluatedAt &&
            request.EvaluatedAt - activity.StartedAt <= policy.ActivityWindow;
        var priorOrderCount = activityIsCurrent ? activity.AcceptedOrderCount : 0;
        var priorTurnover = activityIsCurrent ? activity.AcceptedTurnoverNotional : 0m;
        if (priorOrderCount < 0 || priorTurnover < 0m)
        {
            reasons.Add("RISK_ACTIVITY_CONTEXT_INVALID");
        }
        else
        {
            if (priorOrderCount + 1 > policy.MaxOrdersPerWindow)
            {
                reasons.Add("RISK_ORDER_RATE_LIMIT_EXCEEDED");
            }

            if (priorTurnover + orderNotional > policy.MaxTurnoverPerWindow)
            {
                reasons.Add("RISK_TURNOVER_LIMIT_EXCEEDED");
            }
        }

        if (reasons.Count != 0)
        {
            return new RiskDecision(
                RiskDecisionOutcome.Deny,
                policy.Version,
                reasons[0],
                reasons.ToArray(),
                orderNotional,
                grossExposure,
                positionFraction,
                sectorFraction,
                request.EvaluatedAt);
        }

        return new RiskDecision(
            RiskDecisionOutcome.Allow,
            policy.Version,
            "RISK_ALLOW",
            ["RISK_ALLOW"],
            orderNotional,
            grossExposure,
            positionFraction,
            sectorFraction,
            request.EvaluatedAt);
    }

    private static RiskDecision Deny(PaperRiskPolicy policy, RiskEvaluationRequest request, string reasonCode) =>
        Deny(policy, request, [reasonCode]);

    private static RiskDecision Deny(PaperRiskPolicy policy, RiskEvaluationRequest request, List<string> reasonCodes) =>
        new(
            RiskDecisionOutcome.Deny,
            policy.Version,
            reasonCodes[0],
            reasonCodes.ToArray(),
            null,
            null,
            null,
            null,
            request.EvaluatedAt);

    private static RiskDecision Hold(PaperRiskPolicy policy, RiskEvaluationRequest request, string reasonCode) =>
        new(
            RiskDecisionOutcome.Hold,
            policy.Version,
            reasonCode,
            [reasonCode],
            null,
            null,
            null,
            null,
            request.EvaluatedAt);

    private static void ValidatePolicy(PaperRiskPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Version) ||
            policy.AllowedInstrumentReferences is null ||
            policy.AllowedInstrumentReferences.Count == 0 ||
            policy.MaxMarketDataAge <= TimeSpan.Zero ||
            policy.MinimumDailyTradedValue < 0m ||
            policy.MaxOrderNotional <= 0m ||
            policy.MaxSinglePositionFraction <= 0m || policy.MaxSinglePositionFraction > 1m ||
            policy.MaxSectorFraction <= 0m || policy.MaxSectorFraction > 1m ||
            policy.MaxGrossExposure <= 0m ||
            policy.MaxOrdersPerWindow <= 0 ||
            policy.MaxTurnoverPerWindow <= 0m ||
            policy.ActivityWindow <= TimeSpan.Zero)
        {
            throw new ArgumentException("Paper risk policy is invalid or incomplete.", nameof(policy));
        }
    }

    private sealed record ProjectedValue(string SectorReference, decimal MarketValue);
}
