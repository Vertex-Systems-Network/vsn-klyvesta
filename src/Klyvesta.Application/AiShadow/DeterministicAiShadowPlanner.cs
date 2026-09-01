using Klyvesta.Application.Compliance;
using Klyvesta.Application.Risk;
using Klyvesta.Domain.AiShadow;
using Klyvesta.Domain.Compliance;
using Klyvesta.Domain.Risk;

namespace Klyvesta.Application.AiShadow;

public enum ShadowOptimizationOutcome
{
    Action,
    NoTrade,
    InvalidEvidence,
}

public sealed record ShadowOptimizationResult(
    ShadowOptimizationOutcome Outcome,
    decimal CurrentWeight,
    decimal TargetWeight,
    RiskTradeSide? Side,
    decimal Quantity,
    decimal AuthoritativePrice,
    string ReasonCode);

public interface IShadowPortfolioOptimizer
{
    ShadowOptimizationResult Optimize(
        AiProposalAction action,
        RiskPortfolioSnapshot portfolio,
        RiskMarketEvidence? marketEvidence,
        ShadowOptimizationPolicy policy);
}

public sealed class DeterministicShadowPortfolioOptimizer : IShadowPortfolioOptimizer
{
    public ShadowOptimizationResult Optimize(
        AiProposalAction action,
        RiskPortfolioSnapshot portfolio,
        RiskMarketEvidence? marketEvidence,
        ShadowOptimizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(policy);
        ValidatePolicy(policy);

        if (string.IsNullOrWhiteSpace(action.InstrumentReference))
        {
            return Invalid("AI_OPTIMIZER_INSTRUMENT_INVALID");
        }

        if (!TryCalculateNav(portfolio, out var nav) || nav <= 0m)
        {
            return Invalid("AI_OPTIMIZER_PORTFOLIO_NAV_INVALID");
        }

        var matchingPositions = portfolio.Positions
            .Where(position => StringComparer.Ordinal.Equals(position.InstrumentReference, action.InstrumentReference))
            .ToArray();
        if (matchingPositions.Length > 1)
        {
            return Invalid("AI_OPTIMIZER_DUPLICATE_POSITION");
        }

        var currentQuantity = matchingPositions.Length == 1 ? matchingPositions[0].Quantity : 0m;
        var referencePrice = marketEvidence?.LastPrice ?? matchingPositions.FirstOrDefault()?.MarketPrice ?? 0m;
        var currentWeight = referencePrice > 0m ? currentQuantity * referencePrice / nav : 0m;

        if (action.Action == AiProposalActionKind.Hold)
        {
            return new ShadowOptimizationResult(
                ShadowOptimizationOutcome.NoTrade,
                currentWeight,
                currentWeight,
                null,
                0m,
                referencePrice,
                "AI_OPTIMIZER_HOLD");
        }

        if (action.Action != AiProposalActionKind.TargetAllocation || action.TargetWeight is null)
        {
            return Invalid("AI_OPTIMIZER_ACTION_INVALID");
        }

        if (marketEvidence is null ||
            !StringComparer.Ordinal.Equals(marketEvidence.InstrumentReference, action.InstrumentReference) ||
            marketEvidence.LastPrice <= 0m)
        {
            return Invalid("AI_OPTIMIZER_MARKET_EVIDENCE_INVALID");
        }

        var targetWeight = action.TargetWeight.Value;
        if (targetWeight is < 0m or > 1m)
        {
            return Invalid("AI_OPTIMIZER_TARGET_WEIGHT_INVALID");
        }

        var rawTargetQuantity = nav * targetWeight / marketEvidence.LastPrice;
        var targetQuantity = Math.Floor(rawTargetQuantity / policy.QuantityIncrement) * policy.QuantityIncrement;
        var delta = targetQuantity - currentQuantity;
        if (delta == 0m)
        {
            return new ShadowOptimizationResult(
                ShadowOptimizationOutcome.NoTrade,
                currentWeight,
                targetWeight,
                null,
                0m,
                marketEvidence.LastPrice,
                "AI_OPTIMIZER_NO_DELTA");
        }

        var quantity = Math.Abs(delta);
        var orderNotional = quantity * marketEvidence.LastPrice;
        if (orderNotional < policy.MinimumOrderNotional)
        {
            return new ShadowOptimizationResult(
                ShadowOptimizationOutcome.NoTrade,
                currentWeight,
                targetWeight,
                null,
                0m,
                marketEvidence.LastPrice,
                "AI_OPTIMIZER_BELOW_MINIMUM_NOTIONAL");
        }

        return new ShadowOptimizationResult(
            ShadowOptimizationOutcome.Action,
            currentWeight,
            targetWeight,
            delta > 0m ? RiskTradeSide.Buy : RiskTradeSide.Sell,
            quantity,
            marketEvidence.LastPrice,
            "AI_OPTIMIZER_ACTION_READY");
    }

    private static bool TryCalculateNav(RiskPortfolioSnapshot portfolio, out decimal nav)
    {
        nav = portfolio.Cash;
        if (portfolio.Cash < 0m)
        {
            return false;
        }

        foreach (var position in portfolio.Positions)
        {
            if (position.Quantity < 0m || position.MarketPrice <= 0m)
            {
                return false;
            }

            nav += position.Quantity * position.MarketPrice;
        }

        return true;
    }

    private static ShadowOptimizationResult Invalid(string reasonCode) =>
        new(
            ShadowOptimizationOutcome.InvalidEvidence,
            0m,
            0m,
            null,
            0m,
            0m,
            reasonCode);

    private static void ValidatePolicy(ShadowOptimizationPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Version) ||
            policy.QuantityIncrement <= 0m ||
            policy.MinimumOrderNotional < 0m)
        {
            throw new ArgumentException("Shadow optimization policy is invalid or incomplete.", nameof(policy));
        }
    }
}

public sealed record AiShadowComplianceContext(
    PaperCompliancePolicy Policy,
    PaperExecutionMode ExecutionMode,
    ComplianceAccountStatus AccountStatus,
    RegulatoryFeatureStatus RegulatoryFeatureStatus,
    ManualReviewStatus ManualReviewStatus,
    ComplianceMandateEvidence? Mandate,
    IReadOnlyDictionary<string, InstrumentRestrictionStatus> InstrumentRestrictions);

public sealed record AiShadowAuthoritativeContext(
    string PortfolioContextReference,
    string CustomerContextReference,
    IReadOnlySet<string> EvidenceReferences,
    RiskPortfolioSnapshot Portfolio,
    IReadOnlyDictionary<string, RiskInstrumentContext> Instruments,
    IReadOnlyDictionary<string, RiskMarketEvidence> MarketEvidence,
    RiskActivityWindow Activity,
    PaperRiskPolicy RiskPolicy,
    AiShadowComplianceContext Compliance);

public sealed class DeterministicAiShadowPlanner
{
    private readonly DeterministicAiProposalValidator _validator;
    private readonly IShadowPortfolioOptimizer _optimizer;
    private readonly IRiskGovernor _riskGovernor;
    private readonly IComplianceGate _complianceGate;

    public DeterministicAiShadowPlanner(
        DeterministicAiProposalValidator validator,
        IShadowPortfolioOptimizer optimizer,
        IRiskGovernor riskGovernor,
        IComplianceGate complianceGate)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _riskGovernor = riskGovernor ?? throw new ArgumentNullException(nameof(riskGovernor));
        _complianceGate = complianceGate ?? throw new ArgumentNullException(nameof(complianceGate));
    }

    public AiShadowRunResult Plan(
        AiInvestmentProposal proposal,
        AiProposalValidationPolicy validationPolicy,
        ShadowOptimizationPolicy optimizationPolicy,
        AiShadowAuthoritativeContext context,
        DateTimeOffset plannedAt)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(validationPolicy);
        ArgumentNullException.ThrowIfNull(optimizationPolicy);
        ArgumentNullException.ThrowIfNull(context);

        var audit = new List<AiShadowAuditEvent>();
        var validation = _validator.Validate(proposal, validationPolicy, plannedAt);
        var contextErrors = ValidateAuthoritativeContext(proposal, context);
        var errors = validation.Errors.Concat(contextErrors).ToArray();
        if (errors.Length > 0)
        {
            audit.Add(new AiShadowAuditEvent(
                "AI_PROPOSAL_REJECTED",
                plannedAt,
                string.Join(',', errors)));
            return new AiShadowRunResult(AiShadowRunState.InvalidProposal, null, errors, audit.ToArray());
        }

        audit.Add(new AiShadowAuditEvent(
            "AI_PROPOSAL_VALIDATED",
            plannedAt,
            $"proposal:{proposal.ProposalId:N};model:{proposal.ModelVersion};prompt:{proposal.PromptVersion};validation:{validationPolicy.Version}"));

        var items = new List<AiShadowPlanItem>();
        var workingPortfolio = context.Portfolio;
        var workingActivity = context.Activity;

        for (var index = 0; index < proposal.Actions.Count; index++)
        {
            var action = proposal.Actions[index];
            context.MarketEvidence.TryGetValue(action.InstrumentReference, out var market);
            var optimized = _optimizer.Optimize(action, workingPortfolio, market, optimizationPolicy);

            if (optimized.Outcome == ShadowOptimizationOutcome.InvalidEvidence)
            {
                items.Add(ToItem(action, optimized, ShadowPlanItemStatus.InvalidEvidence, optimized.ReasonCode, null, null));
                audit.Add(Audit(index, action.InstrumentReference, "AI_OPTIMIZATION_BLOCKED", optimized.ReasonCode, plannedAt));
                continue;
            }

            if (optimized.Outcome == ShadowOptimizationOutcome.NoTrade)
            {
                items.Add(ToItem(action, optimized, ShadowPlanItemStatus.NoTrade, optimized.ReasonCode, null, null));
                audit.Add(Audit(index, action.InstrumentReference, "AI_OPTIMIZATION_NO_TRADE", optimized.ReasonCode, plannedAt));
                continue;
            }

            context.Instruments.TryGetValue(action.InstrumentReference, out var instrument);
            RiskEvaluationRequest riskRequest = new(
                workingPortfolio.AccountReference,
                action.InstrumentReference,
                optimized.Side == RiskTradeSide.Buy ? RiskTradeSide.Buy : RiskTradeSide.Sell,
                optimized.Quantity,
                optimized.AuthoritativePrice,
                UsesMargin: false,
                RequestedLeverageMultiple: 1m,
                instrument,
                market,
                workingPortfolio,
                workingActivity,
                plannedAt);

            var riskDecision = _riskGovernor.Evaluate(context.RiskPolicy, riskRequest);
            if (riskDecision.Outcome != RiskDecisionOutcome.Allow)
            {
                items.Add(ToItem(
                    action,
                    optimized,
                    ShadowPlanItemStatus.BlockedRisk,
                    riskDecision.PrimaryReasonCode,
                    riskDecision,
                    null));
                audit.Add(Audit(index, action.InstrumentReference, "AI_RISK_BLOCKED", riskDecision.PrimaryReasonCode, plannedAt));
                continue;
            }

            var restriction = context.Compliance.InstrumentRestrictions.TryGetValue(action.InstrumentReference, out var configuredRestriction)
                ? configuredRestriction
                : InstrumentRestrictionStatus.Unknown;
            ComplianceEvaluationRequest complianceRequest = new(
                workingPortfolio.AccountReference,
                action.InstrumentReference,
                context.Compliance.ExecutionMode,
                context.Compliance.AccountStatus,
                context.Compliance.RegulatoryFeatureStatus,
                context.Compliance.ManualReviewStatus,
                restriction,
                context.Compliance.Mandate,
                plannedAt);
            var complianceDecision = _complianceGate.Evaluate(context.Compliance.Policy, complianceRequest);
            if (complianceDecision.Outcome != ComplianceDecisionOutcome.Allow)
            {
                items.Add(ToItem(
                    action,
                    optimized,
                    ShadowPlanItemStatus.BlockedCompliance,
                    complianceDecision.PrimaryReasonCode,
                    riskDecision,
                    complianceDecision));
                audit.Add(Audit(index, action.InstrumentReference, "AI_COMPLIANCE_BLOCKED", complianceDecision.PrimaryReasonCode, plannedAt));
                continue;
            }

            items.Add(ToItem(
                action,
                optimized,
                ShadowPlanItemStatus.ReadyForPaper,
                "AI_SHADOW_READY_FOR_PAPER",
                riskDecision,
                complianceDecision));
            audit.Add(Audit(index, action.InstrumentReference, "AI_SHADOW_READY", "AI_SHADOW_READY_FOR_PAPER", plannedAt));

            workingPortfolio = ApplyPlannedTrade(workingPortfolio, instrument!, market!, optimized);
            workingActivity = new RiskActivityWindow(
                workingActivity.StartedAt,
                workingActivity.AcceptedOrderCount + 1,
                workingActivity.AcceptedTurnoverNotional + optimized.Quantity * optimized.AuthoritativePrice);
        }

        audit.Add(new AiShadowAuditEvent(
            "AI_SHADOW_PLAN_CREATED",
            plannedAt,
            $"proposal:{proposal.ProposalId:N};items:{items.Count};optimizer:{optimizationPolicy.Version};risk:{context.RiskPolicy.Version};compliance:{context.Compliance.Policy.Version}"));

        AiShadowPlan plan = new(
            proposal.ProposalId,
            context.Portfolio.AccountReference,
            proposal.PortfolioContextReference,
            proposal.CustomerContextReference,
            proposal.ModelVersion,
            proposal.PromptVersion,
            validationPolicy.Version,
            optimizationPolicy.Version,
            context.RiskPolicy.Version,
            context.Compliance.Policy.Version,
            proposal.GeneratedAt,
            plannedAt,
            proposal.EvidenceReferences.ToArray(),
            items.ToArray(),
            audit.ToArray());

        return new AiShadowRunResult(AiShadowRunState.Planned, plan, [], audit.ToArray());
    }

    private static List<string> ValidateAuthoritativeContext(
        AiInvestmentProposal proposal,
        AiShadowAuthoritativeContext context)
    {
        var errors = new List<string>();
        if (!StringComparer.Ordinal.Equals(proposal.PortfolioContextReference, context.PortfolioContextReference))
        {
            errors.Add("AI_PORTFOLIO_CONTEXT_MISMATCH");
        }

        if (!StringComparer.Ordinal.Equals(proposal.CustomerContextReference, context.CustomerContextReference))
        {
            errors.Add("AI_CUSTOMER_CONTEXT_MISMATCH");
        }

        if (context.EvidenceReferences is null ||
            proposal.EvidenceReferences.Any(reference => !context.EvidenceReferences.Contains(reference)))
        {
            errors.Add("AI_EVIDENCE_REFERENCE_NOT_AUTHORITATIVE");
        }

        if (context.Portfolio is null || string.IsNullOrWhiteSpace(context.Portfolio.AccountReference))
        {
            errors.Add("AI_AUTHORITATIVE_PORTFOLIO_MISSING");
        }

        if (context.Instruments is null || context.MarketEvidence is null || context.Activity is null ||
            context.RiskPolicy is null || context.Compliance is null || context.Compliance.Policy is null ||
            context.Compliance.InstrumentRestrictions is null)
        {
            errors.Add("AI_AUTHORITATIVE_CONTROL_CONTEXT_INCOMPLETE");
        }

        if (context.Compliance is not null && context.Compliance.ExecutionMode != PaperExecutionMode.AutoSimulation)
        {
            errors.Add("AI_SHADOW_REQUIRES_AUTO_SIMULATION_MODE");
        }

        return errors;
    }

    private static AiShadowPlanItem ToItem(
        AiProposalAction action,
        ShadowOptimizationResult optimized,
        ShadowPlanItemStatus status,
        string reasonCode,
        RiskDecision? riskDecision,
        ComplianceDecision? complianceDecision) =>
        new(
            action.InstrumentReference,
            optimized.TargetWeight,
            optimized.CurrentWeight,
            optimized.Side,
            optimized.Quantity,
            optimized.AuthoritativePrice,
            status,
            reasonCode,
            riskDecision,
            complianceDecision);

    private static AiShadowAuditEvent Audit(
        int index,
        string instrumentReference,
        string code,
        string reasonCode,
        DateTimeOffset occurredAt) =>
        new(code, occurredAt, $"item:{index:D3};instrument:{instrumentReference};reason:{reasonCode}");

    private static RiskPortfolioSnapshot ApplyPlannedTrade(
        RiskPortfolioSnapshot portfolio,
        RiskInstrumentContext instrument,
        RiskMarketEvidence market,
        ShadowOptimizationResult optimized)
    {
        var current = portfolio.Positions
            .FirstOrDefault(position => StringComparer.Ordinal.Equals(position.InstrumentReference, instrument.InstrumentReference));
        var currentQuantity = current?.Quantity ?? 0m;
        var signedQuantity = optimized.Side == RiskTradeSide.Buy ? optimized.Quantity : -optimized.Quantity;
        var newQuantity = currentQuantity + signedQuantity;
        var notional = optimized.Quantity * optimized.AuthoritativePrice;
        var newCash = optimized.Side == RiskTradeSide.Buy
            ? portfolio.Cash - notional
            : portfolio.Cash + notional;

        var positions = portfolio.Positions
            .Where(position => !StringComparer.Ordinal.Equals(position.InstrumentReference, instrument.InstrumentReference))
            .ToList();
        if (newQuantity > 0m)
        {
            positions.Add(new RiskValuedPosition(
                instrument.InstrumentReference,
                instrument.SectorReference,
                newQuantity,
                optimized.AuthoritativePrice,
                market.ObservedAt));
        }

        return new RiskPortfolioSnapshot(portfolio.AccountReference, newCash, positions.ToArray());
    }
}

public sealed record AiProposalGenerationRequest(
    string PortfolioContextReference,
    string CustomerContextReference,
    IReadOnlyList<string> EvidenceReferences);

public interface IAiProposalSource
{
    Task<string> GenerateProposalJsonAsync(
        AiProposalGenerationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AiShadowOrchestrator
{
    private readonly IAiProposalSource _proposalSource;
    private readonly AiProposalJsonParser _parser;
    private readonly DeterministicAiShadowPlanner _planner;

    public AiShadowOrchestrator(
        IAiProposalSource proposalSource,
        AiProposalJsonParser parser,
        DeterministicAiShadowPlanner planner)
    {
        _proposalSource = proposalSource ?? throw new ArgumentNullException(nameof(proposalSource));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
    }

    public async Task<AiShadowRunResult> RunAsync(
        AiProposalGenerationRequest request,
        AiProposalValidationPolicy validationPolicy,
        ShadowOptimizationPolicy optimizationPolicy,
        AiShadowAuthoritativeContext context,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string json;
        try
        {
            json = await _proposalSource.GenerateProposalJsonAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var audit = new AiShadowAuditEvent(
                "AI_MODEL_UNAVAILABLE",
                evaluatedAt,
                exception.GetType().Name);
            return new AiShadowRunResult(
                AiShadowRunState.ModelUnavailable,
                null,
                ["AI_MODEL_UNAVAILABLE"],
                [audit]);
        }

        var parsed = _parser.Parse(json);
        if (!parsed.IsValid)
        {
            var audit = new AiShadowAuditEvent(
                "AI_PROPOSAL_PARSE_REJECTED",
                evaluatedAt,
                string.Join(',', parsed.Errors));
            return new AiShadowRunResult(
                AiShadowRunState.InvalidProposal,
                null,
                parsed.Errors,
                [audit]);
        }

        return _planner.Plan(parsed.Proposal!, validationPolicy, optimizationPolicy, context, evaluatedAt);
    }
}
