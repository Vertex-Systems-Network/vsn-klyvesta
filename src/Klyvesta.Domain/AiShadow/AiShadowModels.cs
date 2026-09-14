using Klyvesta.Domain.Compliance;
using Klyvesta.Domain.Risk;

namespace Klyvesta.Domain.AiShadow;

public enum AiProposalActionKind
{
    TargetAllocation,
    Hold,
}

public sealed record AiProposalAction(
    string InstrumentReference,
    AiProposalActionKind Action,
    decimal? TargetWeight);

public sealed record AiInvestmentProposal(
    Guid ProposalId,
    string PortfolioContextReference,
    string CustomerContextReference,
    IReadOnlyList<AiProposalAction> Actions,
    IReadOnlyList<string> EvidenceReferences,
    decimal Confidence,
    decimal Uncertainty,
    string ModelVersion,
    string PromptVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset DataObservedAt,
    string Explanation);

public sealed record AiProposalValidationPolicy(
    string Version,
    TimeSpan MaxProposalAge,
    TimeSpan MaxDataAge,
    TimeSpan MaxFutureSkew,
    int MaxActions,
    int MaxEvidenceReferences,
    int MaxExplanationLength,
    decimal MaxTargetWeight);

public sealed record ShadowOptimizationPolicy(
    string Version,
    decimal QuantityIncrement,
    decimal MinimumOrderNotional);

public enum ShadowPlanItemStatus
{
    NoTrade,
    ReadyForPaper,
    BlockedRisk,
    BlockedCompliance,
    InvalidEvidence,
}

public sealed record AiShadowPlanItem(
    string InstrumentReference,
    decimal TargetWeight,
    decimal CurrentWeight,
    RiskTradeSide? Side,
    decimal Quantity,
    decimal AuthoritativePrice,
    ShadowPlanItemStatus Status,
    string PrimaryReasonCode,
    RiskDecision? RiskDecision,
    ComplianceDecision? ComplianceDecision);

public sealed record AiShadowAuditEvent(
    string Code,
    DateTimeOffset OccurredAt,
    string Reference);

public sealed record AiShadowPlan(
    Guid ProposalId,
    string AccountReference,
    string PortfolioContextReference,
    string CustomerContextReference,
    string ModelVersion,
    string PromptVersion,
    string ValidationPolicyVersion,
    string OptimizationPolicyVersion,
    string RiskPolicyVersion,
    string CompliancePolicyVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset PlannedAt,
    IReadOnlyList<string> EvidenceReferences,
    IReadOnlyList<AiShadowPlanItem> Items,
    IReadOnlyList<AiShadowAuditEvent> AuditEvents);

public enum AiShadowRunState
{
    Planned,
    InvalidProposal,
    ModelUnavailable,
}

public sealed record AiShadowRunResult(
    AiShadowRunState State,
    AiShadowPlan? Plan,
    IReadOnlyList<string> Errors,
    IReadOnlyList<AiShadowAuditEvent> AuditEvents);
