using System;
using Klyvesta.Domain.Common;
using Klyvesta.Domain.Risk;
using Klyvesta.Domain.Compliance;

namespace Klyvesta.Domain.Persistence.Entities;

/// <summary>
/// PostgreSQL entity for RiskPolicy with versioning.
/// Each decision references the exact policy version used.
/// </summary>
public class RiskPolicyEntity : IObserved
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Semantic version (e.g., "1.0.0")
    /// </summary>
    public string Version { get; set; } = "1.0.0";
    
    /// <summary>
    /// Human-readable description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Policy is active and should be enforced
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Effective date (policies can be future-dated)
    /// </summary>
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Expiration date (null = indefinite)
    /// </summary>
    public DateTime? EffectiveToUtc { get; set; }
    
    // Concentration limits (stored as basis points, e.g., 500 = 5.00%)
    public int MaxSinglePositionConcentrationBps { get; set; } = 1000; // 10%
    public int MaxSectorConcentrationBps { get; set; } = 3000; // 30%
    public int MaxIndustryConcentrationBps { get; set; } = 5000; // 50%
    
    // Liquidity requirements
    public long MinMarketCapMinorUnits { get; set; } = 300_000_000_00; // $300M
    public long MinAverageDailyVolume { get; set; } = 100_000; // shares
    
    // Order limits
    public long MaxOrderValueMinorUnits { get; set; } = 100_000_00; // $100K
    public int MaxOrderPercentOfAdvBps { get; set; } = 100; // 1% of ADV
    
    // Portfolio limits
    public int MaxPortfolioExposureBps { get; set; } = 10000; // 100% (no leverage)
    public int MinCashBufferBps { get; set; } = 500; // 5%
    public int MaxTurnoverAnnualBps { get; set; } = 30000; // 300% annual turnover
    
    // Prohibited behaviors (boolean flags)
    public bool AllowLeverage { get; set; } = false;
    public bool AllowMargin { get; set; } = false;
    public bool AllowShorting { get; set; } = false;
    public bool AllowDerivatives { get; set; } = false;
    public bool AllowPennyStocks { get; set; } = false;
    
    // Market data freshness (seconds)
    public int MaxDataAgeSeconds { get; set; } = 300; // 5 minutes
    
    // Kill switch
    public bool KillSwitchEnabled { get; set; } = false;
    
    /// <summary>
    /// JSON blob for complex rules not covered by scalar fields
    /// </summary>
    public string? AdditionalRulesJson { get; set; }
    
    /// <summary>
    /// Who created/updated this policy version
    /// </summary>
    public string CreatedBy { get; set; } = "system";
    public string UpdatedBy { get; set; } = "system";
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
}

/// <summary>
/// PostgreSQL entity for RiskDecision audit trail.
/// Every risk check result is persisted for compliance and debugging.
/// </summary>
public class RiskDecisionEntity : IObserved
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Strong-typed identifier
    /// </summary>
    public string DecisionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Reference to order intent being evaluated
    /// </summary>
    public Guid OrderIntentId { get; set; }
    public string OrderIntentIdRef { get; set; } = string.Empty;
    
    /// <summary>
    /// Reference to customer/account context
    /// </summary>
    public Guid CustomerId { get; set; }
    public string CustomerIdRef { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string AccountIdRef { get; set; } = string.Empty;
    
    /// <summary>
    /// Policy version used for this decision
    /// </summary>
    public string RiskPolicyVersion { get; set; } = string.Empty;
    public Guid RiskPolicyId { get; set; }
    
    /// <summary>
    /// Decision outcome
    /// </summary>
    public RiskDecisionType Decision { get; set; }
    
    /// <summary>
    /// Individual check results (JSON array)
    /// </summary>
    public string CheckResultsJson { get; set; } = "[]";
    
    /// <summary>
    /// Overall reason for denial (if denied)
    /// </summary>
    public string? DenialReason { get; set; }
    
    /// <summary>
    /// Portfolio snapshot at time of decision (JSON)
    /// </summary>
    public string? PortfolioSnapshotJson { get; set; }
    
    /// <summary>
    /// Correlation ID for tracing
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
}

/// <summary>
/// PostgreSQL entity for CompliancePolicy with versioning.
/// </summary>
public class CompliancePolicyEntity : IObserved
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Semantic version
    /// </summary>
    public string Version { get; set; } = "1.0.0";
    
    /// <summary>
    /// Description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Policy is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Effective date range
    /// </summary>
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    
    // Regulatory feature gates
    public bool AllowAutoTrading { get; set; } = false;
    public bool AllowMarketDataAccess { get; set; } = true;
    public bool AllowOptionsTrading { get; set; } = false;
    public bool AllowCryptoTrading { get; set; } = false;
    
    // Account state requirements
    public bool RequireVerifiedIdentity { get; set; } = true;
    public bool RequireLinkedBankAccount { get; set; } = true;
    public bool RequireSignedMandateForAuto { get; set; } = true;
    
    // Instrument restrictions (comma-separated lists or JSON)
    public string? AllowedInstrumentTypes { get; set; } // e.g., "equity,etf"
    public string? ProhibitedInstrumentTypes { get; set; }
    public string? AllowedSectors { get; set; }
    public string? ProhibitedSectors { get; set; }
    
    // Manual review settings
    public bool ManualReviewRequired { get; set; } = false;
    public int? ManualReviewThresholdMinorUnits { get; set; }
    
    /// <summary>
    /// Complex rules as JSON
    /// </summary>
    public string? AdditionalRulesJson { get; set; }
    
    public string CreatedBy { get; set; } = "system";
    public string UpdatedBy { get; set; } = "system";
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
}

/// <summary>
/// PostgreSQL entity for Mandate (customer authorization for auto-trading).
/// </summary>
public class MandateEntity : IObserved
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Strong-typed identifier
    /// </summary>
    public string MandateId { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer who granted the mandate
    /// </summary>
    public Guid CustomerId { get; set; }
    public string CustomerIdRef { get; set; } = string.Empty;
    
    /// <summary>
    /// Account covered by this mandate
    /// </summary>
    public Guid AccountId { get; set; }
    public string AccountIdRef { get; set; } = string.Empty;
    
    /// <summary>
    /// Mandate status
    /// </summary>
    public MandateStatus Status { get; set; } = MandateStatus.Active;
    
    /// <summary>
    /// How customer accepted (e.g., "electronic_signature", "clickwrap")
    /// </summary>
    public string AcceptanceMethod { get; set; } = string.Empty;
    
    /// <summary>
    /// IP address at acceptance
    /// </summary>
    public string AcceptanceIpAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// Device fingerprint at acceptance
    /// </summary>
    public string AcceptanceDeviceFingerprint { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp of acceptance
    /// </summary>
    public DateTime AcceptedAtUtc { get; set; }
    
    /// <summary>
    /// Allowed instruments (JSON array)
    /// </summary>
    public string? AllowedInstrumentsJson { get; set; }
    
    /// <summary>
    /// Prohibited instruments (JSON array)
    /// </summary>
    public string? ProhibitedInstrumentsJson { get; set; }
    
    /// <summary>
    /// Allowed sectors (JSON array)
    /// </summary>
    public string? AllowedSectorsJson { get; set; }
    
    /// <summary>
    /// Prohibited sectors (JSON array)
    /// </summary>
    public string? ProhibitedSectorsJson { get; set; }
    
    // Risk parameters from mandate
    public int InvestmentHorizonMonths { get; set; } = 12;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Moderate;
    public int MaxConcentrationBps { get; set; } = 1000; // 10%
    public int MaxTurnoverAnnualBps { get; set; } = 30000; // 300%
    public int MaxDrawdownToleranceBps { get; set; } = 2000; // 20%
    
    /// <summary>
    /// What to do when drawdown limit approached
    /// </summary>
    public DrawdownResponsePolicy DrawdownResponse { get; set; } = DrawdownResponsePolicy.NotifyOnly;
    
    /// <summary>
    /// Suspension reason (if suspended)
    /// </summary>
    public string? SuspensionReason { get; set; }
    
    /// <summary>
    /// Revocation reason (if revoked)
    /// </summary>
    public string? RevocationReason { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
    
    /// <summary>
    /// Navigation: compliance decisions referencing this mandate
    /// </summary>
    public virtual ICollection<ComplianceDecisionEntity> ComplianceDecisions { get; set; } = new List<ComplianceDecisionEntity>();
}

/// <summary>
/// PostgreSQL entity for ComplianceDecision audit trail.
/// </summary>
public class ComplianceDecisionEntity : IObserved
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Strong-typed identifier
    /// </summary>
    public string DecisionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Reference to order intent
    /// </summary>
    public Guid OrderIntentId { get; set; }
    public string OrderIntentIdRef { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer/account context
    /// </summary>
    public Guid CustomerId { get; set; }
    public string CustomerIdRef { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string AccountIdRef { get; set; } = string.Empty;
    
    /// <summary>
    /// Policy version used
    /// </summary>
    public string CompliancePolicyVersion { get; set; } = string.Empty;
    public Guid CompliancePolicyId { get; set; }
    
    /// <summary>
    /// Mandate reference (if applicable)
    /// </summary>
    public Guid? MandateId { get; set; }
    public string? MandateIdRef { get; set; }
    
    /// <summary>
    /// Decision outcome
    /// </summary>
    public ComplianceDecisionType Decision { get; set; }
    
    /// <summary>
    /// Check results (JSON array)
    /// </summary>
    public string CheckResultsJson { get; set; } = "[]";
    
    /// <summary>
    /// Denial reason (if denied)
    /// </summary>
    public string? DenialReason { get; set; }
    
    /// <summary>
    /// Regulatory references for denial
    /// </summary>
    public string? RegulatoryReferences { get; set; }
    
    /// <summary>
    /// Manual review flag
    /// </summary>
    public bool RequiresManualReview { get; set; }
    
    /// <summary>
    /// Reviewer notes (if manually reviewed)
    /// </summary>
    public string? ManualReviewNotes { get; set; }
    public string? ManualReviewerId { get; set; }
    public DateTime? ManualReviewedAtUtc { get; set; }
    
    /// <summary>
    /// Correlation ID
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
    
    /// <summary>
    /// Navigation: back to mandate
    /// </summary>
    public virtual MandateEntity? Mandate { get; set; }
}
