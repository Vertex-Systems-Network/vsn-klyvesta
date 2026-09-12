namespace Klyvesta.Domain.Common;

/// <summary>
/// Base interface for all domain entities with UUIDv7 identifiers.
/// </summary>
public interface IEntity
{
    DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Generic interface for domain entities with strongly-typed IDs.
/// </summary>
public interface IEntity<TId> : IEntity
{
    TId Id { get; }
}

/// <summary>
/// Represents a point-in-time observation with source timestamp and observed timestamp.
/// </summary>
public interface IObserved
{
    /// <summary>
    /// When the external system recorded this event/observation.
    /// </summary>
    DateTime? ExternalTimestampUtc { get; }
    
    /// <summary>
    /// When Klyvesta observed/recorded this event.
    /// </summary>
    DateTime ObservedAtUtc { get; }
    
    /// <summary>
    /// When this entity was created (for Added entities).
    /// </summary>
    DateTime CreatedAtUtc { get; set; }
    
    /// <summary>
    /// When this entity was last updated (for Modified entities).
    /// </summary>
    DateTime UpdatedAtUtc { get; set; }
}

/// <summary>
/// Result state for broker operations.
/// </summary>
public enum BrokerResultState
{
    Success = 0,
    Rejected = 1,
    RetryableFailure = 2,
    Unknown = 3
}

/// <summary>
/// Normalized order states independent of broker-specific statuses.
/// </summary>
public enum OrderState
{
    PendingSubmit = 0,
    Submitted = 1,
    Open = 2,
    PartiallyFilled = 3,
    Filled = 4,
    CancelPending = 5,
    Cancelled = 6,
    Rejected = 7,
    Unknown = 8
}

/// <summary>
/// Side of an order or position.
/// </summary>
public enum Side
{
    Buy = 0,
    Sell = 1
}

/// <summary>
/// Time in force for orders.
/// </summary>
public enum TimeInForce
{
    Day = 0,
    Gtc = 1, // Good till cancelled
    Ioc = 2, // Immediate or cancel
    Fog = 3  // Fill or kill
}

/// <summary>
/// Account status normalized across brokers.
/// </summary>
public enum AccountStatus
{
    Pending = 0,
    Active = 1,
    Restricted = 2,
    Suspended = 3,
    Closed = 4,
    Rejected = 5,
    Unknown = 6
}

/// <summary>
/// Investment mode for customer portfolios.
/// </summary>
public enum InvestmentMode
{
    Manual = 0,
    AiAssisted = 1,
    GuardedAuto = 2
}

/// <summary>
/// Risk decision outcome from the Risk Governor.
/// </summary>
public enum RiskDecisionType
{
    Pass = 0,
    Fail = 1,
    Deny = 2
}

/// <summary>
/// Compliance decision outcome from the Compliance Gate.
/// </summary>
public enum ComplianceDecisionType
{
    Pass = 0,
    ReviewRequired = 1,
    Deny = 2
}

/// <summary>
/// Order type for execution instructions.
/// </summary>
public enum OrderType
{
    Market = 0,
    Limit = 1,
    StopLoss = 2,
    StopLimit = 3
}

/// <summary>
/// Risk level classification for policies and decisions.
/// </summary>
public enum RiskLevel
{
    Low = 0,
    Moderate = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Policy for responding to drawdown events.
/// </summary>
public enum DrawdownResponsePolicy
{
    NotifyOnly = 0,
    ReducePositions = 1,
    HaltTrading = 2,
    LiquidateAll = 3
}
