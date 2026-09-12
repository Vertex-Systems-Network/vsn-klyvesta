namespace Klyvesta.Domain.Risk;

using Klyvesta.Domain.Common;

/// <summary>
/// Result from the Risk Governor - deterministic risk validation.
/// AI cannot override a DENY decision.
/// </summary>
public readonly record struct RiskDecision(
    Guid DecisionId,
    bool IsApproved,
    string? DenialReasonCode,
    string? DenialMessage,
    DateTime DecisionTimestampUtc,
    string PolicyVersion,
    IReadOnlyList<RiskCheckResult> CheckResults,
    CorrelationId CorrelationId
)
{
    public static RiskDecision Approve(Guid decisionId, IReadOnlyList<RiskCheckResult> checks, string policyVersion, CorrelationId correlationId)
        => new(decisionId, true, null, null, DateTime.UtcNow, policyVersion, checks, correlationId);
    
    public static RiskDecision Deny(
        Guid decisionId, 
        string reasonCode, 
        string message, 
        IReadOnlyList<RiskCheckResult> checks, 
        string policyVersion, 
        CorrelationId correlationId)
        => new(decisionId, false, reasonCode, message, DateTime.UtcNow, policyVersion, checks, correlationId);
}

/// <summary>
/// Result of an individual risk check.
/// </summary>
public readonly record struct RiskCheckResult(
    string CheckName,
    bool Passed,
    string? FailureReason,
    decimal? ActualValue,
    decimal? ThresholdValue,
    string? Unit
);

/// <summary>
/// Risk policy configuration - versioned and auditable.
/// All thresholds are configurable per policy version.
/// </summary>
public sealed record RiskPolicy(
    string Version,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    
    // Instrument universe restrictions
    IReadOnlyList<string> AllowedInstrumentTypes,
    IReadOnlyList<string> ProhibitedInstruments,
    
    // Concentration limits (as percentages)
    decimal MaxSinglePositionConcentration,
    decimal MaxSectorConcentration,
    decimal MaxIndustryConcentration,
    
    // Liquidity requirements
    decimal MinAverageDailyVolume,
    decimal? MinMarketCap,
    
    // Order-level limits
    decimal MaxOrderValue,
    decimal MaxOrderQuantityPercentOfADV, // As percentage
    
    // Portfolio-level limits
    decimal MaxPortfolioExposure,
    decimal MinCashBuffer,
    decimal MaxTurnoverPerDay,
    int MaxOrdersPerDay,
    
    // Prohibited behaviors
    bool AllowLeverage,
    bool AllowMargin,
    bool AllowShortSelling,
    bool AllowDerivatives,
    bool AllowPennyStocks,
    
    // Market data freshness
    TimeSpan MaxDataAgeForExecution,
    
    // System-wide controls
    bool KillSwitchEnabled
);

/// <summary>
/// Input context for risk evaluation.
/// </summary>
public readonly record struct RiskEvaluationContext(
    AccountId AccountId,
    InstrumentId InstrumentId,
    Side Side,
    decimal Quantity,
    decimal? Price,
    Money PortfolioValue,
    Money CashBalance,
    IReadOnlyList<PositionSnapshot> ExistingPositions,
    SectorClassification? InstrumentSector,
    IndustryClassification? InstrumentIndustry,
    MarketDataSnapshot? MarketData,
    InvestmentMode InvestmentMode,
    MandateId? MandateId,
    ProposalId? AiProposalId
);

/// <summary>
/// Snapshot of an existing position for risk calculation.
/// </summary>
public readonly record struct PositionSnapshot(
    InstrumentId InstrumentId,
    string Symbol,
    decimal Quantity,
    Money CurrentValue,
    Percentage PortfolioWeight,
    SectorClassification? Sector,
    IndustryClassification? Industry
);

/// <summary>
/// Market data snapshot for risk evaluation.
/// </summary>
public readonly record struct MarketDataSnapshot(
    InstrumentId InstrumentId,
    decimal LastPrice,
    string Currency,
    DateTime PriceTimestampUtc,
    DateTime ReceivedAtUtc,
    decimal? AverageDailyVolume,
    decimal? MarketCap,
    bool IsTradingHalted,
    bool IsMarketClosed
);

/// <summary>
/// Sector classification for concentration checks.
/// </summary>
public readonly record struct SectorClassification(string Code, string Name);

/// <summary>
/// Industry classification for concentration checks.
/// </summary>
public readonly record struct IndustryClassification(string Code, string Name);

/// <summary>
/// Interface for the Risk Governor - deterministic risk validation authority.
/// </summary>
public interface IRiskGovernor
{
    /// <summary>
    /// Evaluates an order intent against current risk policy.
    /// Returns APPROVE or DENY - AI cannot override DENY.
    /// </summary>
    Task<RiskDecision> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the currently active risk policy version.
    /// </summary>
    Task<RiskPolicy> GetActivePolicyAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Activates the kill switch - all subsequent evaluations will DENY.
    /// </summary>
    Task ActivateKillSwitchAsync(string reason, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deactivates the kill switch.
    /// </summary>
    Task DeactivateKillSwitchAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if kill switch is active.
    /// </summary>
    Task<bool> IsKillSwitchActiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Exception thrown when risk check fails.
/// </summary>
public sealed class RiskDeniedException : Exception
{
    public RiskDeniedException(string reasonCode, string message, RiskDecision? Decision = null)
        : base(message)
    {
        ReasonCode = reasonCode;
        this.Decision = Decision;
    }
    
    public string ReasonCode { get; }
    public RiskDecision? Decision { get; }
}

/// <summary>
/// Exception thrown when required data for risk evaluation is stale or missing.
/// </summary>
public sealed class StaleDataException : Exception
{
    public StaleDataException(string message, TimeSpan DataAge)
        : base(message)
    {
        this.DataAge = DataAge;
    }
    
    public TimeSpan DataAge { get; }
}
