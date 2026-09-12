namespace Klyvesta.Domain.Agents;

using Klyvesta.Domain.Common;

/// <summary>
/// AI proposal schema - structured output from AI agents.
/// AI may only produce proposals, never execute directly.
/// </summary>
public sealed class AiProposal : IEntity
{
    public ProposalId Id { get; init; } = ProposalId.New();
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Reference to the customer/account this proposal is for.
    /// </summary>
    public required AccountId AccountId { get; init; }
    public required CustomerId CustomerId { get; init; }
    
    /// <summary>
    /// Type of proposal (e.g., "REBALANCE", "NEW_INVESTMENT", "TAX_LOSS_HARVEST").
    /// </summary>
    public required string ProposalType { get; init; }
    
    /// <summary>
    /// Target actions recommended by AI.
    /// </summary>
    public required IReadOnlyList<ProposedAction> Actions { get; init; }
    
    /// <summary>
    /// Evidence/references used to generate this proposal.
    /// </summary>
    public required IReadOnlyList<EvidenceReference> EvidenceReferences { get; init; }
    
    /// <summary>
    /// AI confidence/uncertainty representation.
    /// </summary>
    public decimal ConfidenceScore { get; init; } // 0.0 to 1.0
    
    /// <summary>
    /// Uncertainty explanation in natural language.
    /// </summary>
    public string? UncertaintyExplanation { get; init; }
    
    /// <summary>
    /// Model and prompt version for audit trail.
    /// </summary>
    public required string ModelVersion { get; init; }
    public required string PromptVersion { get; init; }
    
    /// <summary>
    /// Data freshness at time of proposal generation.
    /// </summary>
    public DateTime DataAsOfUtc { get; init; }
    
    /// <summary>
    /// Expected portfolio impact metrics.
    /// </summary>
    public PortfolioImpact? ExpectedImpact { get; init; }
    
    /// <summary>
    /// Downside scenario analysis.
    /// </summary>
    public IReadOnlyList<ScenarioAnalysis>? ScenarioAnalyses { get; init; }
    
    /// <summary>
    /// Natural language explanation for the customer.
    /// </summary>
    public string? CustomerExplanation { get; init; }
    
    /// <summary>
    /// Risk disclosures that should be presented to customer.
    /// </summary>
    public IReadOnlyList<string>? RiskDisclosures { get; init; }
    
    private ProposalStatus _status = ProposalStatus.Pending;
    public ProposalStatus Status
    {
        get => _status;
        private set => _status = value;
    }
    
    public DateTime? ReviewedAtUtc { get; private set; }
    public bool? IsAccepted { get; private set; }
    public string? RejectionReason { get; private set; }
    
    public CorrelationId? CorrelationId { get; init; }
    
    public void MarkAccepted(DateTime reviewedAt)
    {
        if (_status != ProposalStatus.Pending)
            throw new InvalidOperationException($"Cannot accept proposal in state {_status}");
        
        _status = ProposalStatus.Accepted;
        ReviewedAtUtc = reviewedAt;
        IsAccepted = true;
    }
    
    public void MarkRejected(DateTime reviewedAt, string reason)
    {
        if (_status != ProposalStatus.Pending)
            throw new InvalidOperationException($"Cannot reject proposal in state {_status}");
        
        _status = ProposalStatus.Rejected;
        ReviewedAtUtc = reviewedAt;
        IsAccepted = false;
        RejectionReason = reason;
    }
    
    public void MarkExpired(DateTime expiredAt)
    {
        if (_status != ProposalStatus.Pending)
            throw new InvalidOperationException($"Cannot expire proposal in state {_status}");
        
        _status = ProposalStatus.Expired;
    }
}

/// <summary>
/// A single proposed action within an AI proposal.
/// </summary>
public readonly record struct ProposedAction(
    string ActionType, // "BUY", "SELL", "HOLD", "REBALANCE"
    InstrumentId? InstrumentId,
    string? Symbol,
    decimal? Quantity,
    decimal? TargetAllocationPercent,
    string? Rationale,
    Money? EstimatedCost,
    Money? ExpectedProceeds
);

/// <summary>
/// Reference to evidence used in AI decision-making.
/// </summary>
public readonly record struct EvidenceReference(
    string SourceType, // "MARKET_DATA", "RESEARCH_REPORT", "NEWS_ARTICLE", "FUNDAMENTAL_DATA"
    string SourceId,
    string Title,
    DateTime? PublishedAtUtc,
    string? Url,
    string Summary
);

/// <summary>
/// Expected portfolio impact from a proposal.
/// </summary>
public readonly record struct PortfolioImpact(
    Percentage? ExpectedReturnAnnualized,
    Percentage? ExpectedVolatility,
    Percentage? CurrentPortfolioYield,
    Percentage? NewPortfolioYield,
    decimal? CurrentSharpeRatio,
    decimal? ExpectedSharpeRatio,
    Percentage? MaxDrawdownExpected,
    decimal TurnoverPercent,
    Money EstimatedTransactionCosts,
    Percentage? ConcentrationChange,
    IReadOnlyList<SectorShift>? SectorShifts
);

/// <summary>
/// Sector allocation shift from a proposal.
/// </summary>
public readonly record struct SectorShift(
    string SectorName,
    Percentage CurrentAllocation,
    Percentage NewAllocation
);

/// <summary>
/// Scenario analysis for downside risk.
/// </summary>
public readonly record struct ScenarioAnalysis(
    string ScenarioName,
    string Description,
    Percentage? PortfolioImpact,
    ProbabilityLevel Likelihood // "LOW", "MEDIUM", "HIGH"
);

/// <summary>
/// Probability level for scenario analysis.
/// </summary>
public enum ProbabilityLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>
/// Proposal lifecycle status.
/// </summary>
public enum ProposalStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Expired = 3
}

/// <summary>
/// Interface for AI agent proposal management.
/// </summary>
public interface IAiProposalService
{
    /// <summary>
    /// Creates a new AI proposal after schema validation.
    /// </summary>
    Task<AiProposal> CreateProposalAsync(AiProposal proposal, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets proposal by ID.
    /// </summary>
    Task<AiProposal?> GetProposalAsync(ProposalId proposalId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates proposal schema without creating it.
    /// </summary>
    Task<bool> ValidateProposalSchemaAsync(AiProposal proposal, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Records customer/investor decision on a proposal.
    /// </summary>
    Task RecordDecisionAsync(ProposalId proposalId, bool accepted, string? reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Agent hierarchy - types of AI agents in the system.
/// </summary>
public enum AgentType
{
    InvestorUnderstanding = 0,
    MarketIntelligence = 1,
    SignalRegime = 2,
    PortfolioConstruction = 3,
    Rebalancing = 4,
    Explainability = 5,
    InvestorCoach = 6,
    Operations = 7
}

/// <summary>
/// Metadata about an AI agent invocation.
/// </summary>
public readonly record struct AgentInvocationRecord(
    AgentType AgentType,
    Guid InvocationId,
    DateTime InvokedAtUtc,
    TimeSpan Duration,
    string ModelUsed,
    string PromptVersion,
    int InputTokenCount,
    int OutputTokenCount,
    bool HadError,
    string? ErrorCode,
    CorrelationId CorrelationId
);
