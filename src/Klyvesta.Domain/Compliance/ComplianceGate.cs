namespace Klyvesta.Domain.Compliance;

using Klyvesta.Domain.Common;

/// <summary>
/// Result from the Compliance Gate - deterministic compliance validation.
/// AI cannot override a DENY decision.
/// </summary>
public readonly record struct ComplianceDecision(
    Guid DecisionId,
    bool IsApproved,
    string? DenialReasonCode,
    string? DenialMessage,
    DateTime DecisionTimestampUtc,
    string PolicyVersion,
    IReadOnlyList<ComplianceCheckResult> CheckResults,
    CorrelationId CorrelationId
)
{
    public static ComplianceDecision Approve(Guid decisionId, IReadOnlyList<ComplianceCheckResult> checks, string policyVersion, CorrelationId correlationId)
        => new(decisionId, true, null, null, DateTime.UtcNow, policyVersion, checks, correlationId);
    
    public static ComplianceDecision Deny(
        Guid decisionId, 
        string reasonCode, 
        string message, 
        IReadOnlyList<ComplianceCheckResult> checks, 
        string policyVersion, 
        CorrelationId correlationId)
        => new(decisionId, false, reasonCode, message, DateTime.UtcNow, policyVersion, checks, correlationId);
}

/// <summary>
/// Result of an individual compliance check.
/// </summary>
public readonly record struct ComplianceCheckResult(
    string CheckName,
    bool Passed,
    string? FailureReason,
    string? RegulatoryReference
);

/// <summary>
/// Account compliance status.
/// </summary>
public enum ComplianceStatus
{
    Active = 0,
    PendingReview = 1,
    Restricted = 2,
    Suspended = 3,
    Terminated = 4
}

/// <summary>
/// Compliance policy configuration - versioned and auditable.
/// </summary>
public sealed record CompliancePolicy(
    string Version,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    
    // Regulatory feature gates
    bool AutoTradingEnabled,
    bool MarketDataAccessEnabled,
    
    // Instrument restrictions
    IReadOnlyList<string> RestrictedInstruments,
    IReadOnlyList<string> ProhibitedInstrumentCategories,
    
    // Account state requirements
    IReadOnlyList<ComplianceStatus> AllowedAccountStates,
    
    // Mandate requirements
    bool RequireMandateForAuto,
    IReadOnlyList<string> RequiredMandateClauses,
    
    // Review/hold settings
    bool ManualReviewRequiredForFirstOrder,
    decimal ManualReviewThreshold,
    string? ManualReviewCurrency
);

/// <summary>
/// Input context for compliance evaluation.
/// </summary>
public readonly record struct ComplianceEvaluationContext(
    AccountId AccountId,
    CustomerId CustomerId,
    InstrumentId InstrumentId,
    Side Side,
    decimal Quantity,
    decimal? Price,
    ComplianceStatus AccountComplianceStatus,
    InvestmentMode InvestmentMode,
    MandateId? MandateId,
    ProposalId? AiProposalId,
    bool IsFirstOrder,
    DateTime? LastOrderDateUtc,
    string? InstrumentCategory
);

/// <summary>
/// Mandate definition for Guarded Auto mode.
/// Must be explicitly accepted by customer before auto trading.
/// </summary>
public sealed class Mandate : IEntity
{
    public MandateId Id { get; init; } = MandateId.New();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public required AccountId AccountId { get; init; }
    public required CustomerId CustomerId { get; init; }
    
    /// <summary>
    /// Mandate version/reference document.
    /// </summary>
    public required string MandateVersion { get; init; }
    
    /// <summary>
    /// When customer accepted the mandate.
    /// </summary>
    public DateTime AcceptedAtUtc { get; init; }
    
    /// <summary>
    /// How customer accepted (e.g., "ELECTRONIC_SIGNATURE", "CLICK_ACCEPT").
    /// </summary>
    public required string AcceptanceMethod { get; init; }
    
    /// <summary>
    /// IP address/device info at acceptance time for audit.
    /// </summary>
    public string? AcceptanceIpAddress { get; init; }
    public string? AcceptanceDeviceFingerprint { get; init; }
    
    private MandateStatus _status = MandateStatus.Active;
    public MandateStatus Status
    {
        get => _status;
        private set => _status = value;
    }
    
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevocationReason { get; private set; }
    
    /// <summary>
    /// Allowed instruments/categories in this mandate.
    /// </summary>
    public required IReadOnlyList<string> AllowedInstrumentTypes { get; init; }
    public required IReadOnlyList<string> AllowedSectors { get; init; }
    
    /// <summary>
    /// Risk parameters bound to this mandate.
    /// </summary>
    public required int InvestmentHorizonMonths { get; init; }
    public required string RiskLevel { get; init; } // "CONSERVATIVE", "MODERATE", "AGGRESSIVE"
    public required decimal MaxSingleNameExposurePercent { get; init; }
    public required decimal MaxSectorExposurePercent { get; init; }
    public required decimal MinCashBufferPercent { get; init; }
    public required decimal MaxTurnoverPerYear { get; init; }
    public required int MaxOrdersPerDay { get; init; }
    public required decimal MaxDailyOrderValue { get; init; }
    public required string DrawdownResponsePolicy { get; init; }
    public required string RebalanceRule { get; init; }
    
    /// <summary>
    /// Prohibited instruments/categories.
    /// </summary>
    public required IReadOnlyList<string> ProhibitedInstruments { get; init; }
    public required IReadOnlyList<string> ProhibitedCategories { get; init; }
    
    public void Revoke(string reason)
    {
        if (_status == MandateStatus.Revoked)
            throw new InvalidOperationException($"Mandate {Id} is already revoked");
        
        _status = MandateStatus.Revoked;
        RevokedAtUtc = DateTime.UtcNow;
        RevocationReason = reason;
    }
    
    public void Suspend(string reason)
    {
        if (_status != MandateStatus.Active)
            throw new InvalidOperationException($"Cannot suspend mandate in state {_status}");
        
        _status = MandateStatus.Suspended;
    }
    
    public void Reactivate()
    {
        if (_status != MandateStatus.Suspended)
            throw new InvalidOperationException($"Cannot reactivate mandate in state {_status}");
        
        _status = MandateStatus.Active;
    }
}

/// <summary>
/// Mandate lifecycle status.
/// </summary>
public enum MandateStatus
{
    Active = 0,
    Suspended = 1,
    Revoked = 2
}

/// <summary>
/// Interface for the Compliance Gate - deterministic compliance validation authority.
/// </summary>
public interface IComplianceGate
{
    /// <summary>
    /// Evaluates an order intent against compliance policy.
    /// Returns APPROVE or DENY - AI cannot override DENY.
    /// </summary>
    Task<ComplianceDecision> EvaluateAsync(ComplianceEvaluationContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the currently active compliance policy version.
    /// </summary>
    Task<CompliancePolicy> GetActivePolicyAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets mandate for an account if one exists and is active.
    /// </summary>
    Task<Mandate?> GetActiveMandateAsync(AccountId accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates that a mandate covers the proposed action.
    /// </summary>
    Task<bool> ValidateMandateCoverageAsync(MandateId mandateId, ComplianceEvaluationContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Places an account on compliance hold.
    /// </summary>
    Task PlaceOnHoldAsync(AccountId accountId, string reason, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes compliance hold from an account.
    /// </summary>
    Task RemoveHoldAsync(AccountId accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if an account is on hold.
    /// </summary>
    Task<bool> IsOnHoldAsync(AccountId accountId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exception thrown when compliance check fails.
/// </summary>
public sealed class ComplianceDeniedException : Exception
{
    public ComplianceDeniedException(string reasonCode, string message, ComplianceDecision? Decision = null)
        : base(message)
    {
        ReasonCode = reasonCode;
        this.Decision = Decision;
    }
    
    public string ReasonCode { get; }
    public ComplianceDecision? Decision { get; }
}

/// <summary>
/// Exception thrown when mandate is missing or invalid for requested action.
/// </summary>
public sealed class MandateRequiredException : Exception
{
    public MandateRequiredException(string message) : base(message) { }
}
