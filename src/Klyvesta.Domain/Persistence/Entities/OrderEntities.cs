using System;
using Klyvesta.Domain.Common;
using Klyvesta.Domain.Orders;

namespace Klyvesta.Domain.Persistence.Entities;

/// <summary>
/// PostgreSQL entity for OrderIntent with full lifecycle tracking.
/// Represents a pre-execution order that must pass risk/compliance before broker submission.
/// </summary>
public class OrderIntentEntity : IObserved
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Strongly-typed identifier wrapper
    /// </summary>
    public string OrderIntentId { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer who owns this order intent
    /// </summary>
    public Guid CustomerId { get; set; }
    public string CustomerIdRef { get; set; } = string.Empty;
    
    /// <summary>
    /// Account executing the order
    /// </summary>
    public Guid AccountId { get; set; }
    public string AccountIdRef { get; set; } = string.Empty;
    
    /// <summary>
    /// Instrument being traded
    /// </summary>
    public Guid InstrumentId { get; set; }
    public string InstrumentIdRef { get; set; } = string.Empty;
    
    /// <summary>
    /// Buy or Sell
    /// </summary>
    public Side Side { get; set; }
    
    /// <summary>
    /// Number of shares/units (in minor units for precision)
    /// </summary>
    public long QuantityMinorUnits { get; set; }
    
    /// <summary>
    /// Optional limit price (in minor units)
    /// </summary>
    public long? LimitPriceMinorUnits { get; set; }
    
    /// <summary>
    /// Time in force policy
    /// </summary>
    public TimeInForce TimeInForce { get; set; }
    
    /// <summary>
    /// Current state in lifecycle
    /// </summary>
    public OrderState State { get; set; } = OrderState.PendingSubmit;
    
    /// <summary>
    /// Reference to AI proposal that generated this intent (if applicable)
    /// </summary>
    public Guid? AiProposalId { get; set; }
    public string? AiProposalIdRef { get; set; }
    
    /// <summary>
    /// Reference to customer mandate authorizing auto-trading (if applicable)
    /// </summary>
    public Guid? MandateId { get; set; }
    public string? MandateIdRef { get; set; }
    
    /// <summary>
    /// Risk check passed
    /// </summary>
    public bool RiskCheckPassed { get; set; }
    
    /// <summary>
    /// Risk policy version used for validation
    /// </summary>
    public string? RiskPolicyVersion { get; set; }
    
    /// <summary>
    /// Compliance check passed
    /// </summary>
    public bool ComplianceCheckPassed { get; set; }
    
    /// <summary>
    /// Compliance policy version used for validation
    /// </summary>
    public string? CompliancePolicyVersion { get; set; }
    
    /// <summary>
    /// Broker order ID after submission (null until submitted)
    /// </summary>
    public string? BrokerOrderId { get; set; }
    
    /// <summary>
    /// Broker submission timestamp
    /// </summary>
    public DateTime? SubmittedAtUtc { get; set; }
    
    /// <summary>
    /// Quantity filled so far (in minor units)
    /// </summary>
    public long FilledQuantityMinorUnits { get; set; }
    
    /// <summary>
    /// Average fill price (in minor units)
    /// </summary>
    public long? AverageFillPriceMinorUnits { get; set; }
    
    /// <summary>
    /// Total executed value (in minor units)
    /// </summary>
    public long? ExecutedValueMinorUnits { get; set; }
    
    /// <summary>
    /// Remaining quantity to fill (in minor units)
    /// </summary>
    public long RemainingQuantityMinorUnits { get; set; }
    
    /// <summary>
    /// Cancellation requested timestamp
    /// </summary>
    public DateTime? CancelRequestedAtUtc { get; set; }
    
    /// <summary>
    /// Cancellation confirmed timestamp
    /// </summary>
    public DateTime? CancelledAtUtc { get; set; }
    
    /// <summary>
    /// Rejection reason (if rejected)
    /// </summary>
    public string? RejectReason { get; set; }
    
    /// <summary>
    /// Idempotency key for duplicate detection
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Correlation ID for tracing across services
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Error details (if failed)
    /// </summary>
    public string? ErrorDetails { get; set; }
    
    /// <summary>
    /// Unknown state requires reconciliation
    /// </summary>
    public bool RequiresReconciliation { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
    
    /// <summary>
    /// Navigation: execution records (fills)
    /// </summary>
    public virtual ICollection<OrderExecutionEntity> Executions { get; set; } = new List<OrderExecutionEntity>();
}

/// <summary>
/// PostgreSQL entity for OrderExecution (individual fill record).
/// Tracks each partial or complete fill from the broker.
/// </summary>
public class OrderExecutionEntity : IObserved
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Strongly-typed identifier wrapper
    /// </summary>
    public string ExecutionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Parent order intent
    /// </summary>
    public Guid OrderIntentId { get; set; }
    public virtual OrderIntentEntity OrderIntent { get; set; } = null!;
    
    /// <summary>
    /// Broker-provided execution ID
    /// </summary>
    public string BrokerExecutionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Fill quantity (in minor units)
    /// </summary>
    public long QuantityMinorUnits { get; set; }
    
    /// <summary>
    /// Fill price per share (in minor units)
    /// </summary>
    public long PriceMinorUnits { get; set; }
    
    /// <summary>
    /// Execution value = quantity * price (in minor units)
    /// </summary>
    public long ValueMinorUnits { get; set; }
    
    /// <summary>
    /// Commission/fees charged (in minor units)
    /// </summary>
    public long CommissionMinorUnits { get; set; }
    
    /// <summary>
    /// Currency code
    /// </summary>
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Broker timestamp of execution
    /// </summary>
    public DateTime ExecutedAtUtc { get; set; }
    
    /// <summary>
    /// Settlement date (T+2 for equities)
    /// </summary>
    public DateTime SettlementDateUtc { get; set; }
    
    /// <summary>
    /// Liquidity indicator (maker/taker)
    /// </summary>
    public string? LiquidityIndicator { get; set; }
    
    /// <summary>
    /// Execution venue/market
    /// </summary>
    public string? Venue { get; set; }
    
    /// <summary>
    /// Already posted to ledger
    /// </summary>
    public bool PostedToLedger { get; set; }
    
    /// <summary>
    /// Ledger journal reference (once posted)
    /// </summary>
    public Guid? LedgerJournalId { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
}
