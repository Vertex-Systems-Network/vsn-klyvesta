namespace Klyvesta.Domain.Enums;

/// <summary>
/// Order lifecycle status. Matches state machine transitions.
/// </summary>
public enum OrderStatus
{
    /// <summary>Order intent created, not yet submitted to broker.</summary>
    Pending = 1,
    
    /// <summary>Submitted to broker, awaiting execution.</summary>
    Submitted = 2,
    
    /// <summary>Partially filled, remaining quantity active.</summary>
    PartiallyFilled = 3,
    
    /// <summary>Fully executed.</summary>
    Filled = 4,
    
    /// <summary>Cancelled by user or system before completion.</summary>
    Cancelled = 5,
    
    /// <summary>Rejected by broker or validation.</summary>
    Rejected = 6,
    
    /// <summary>Unknown state after ambiguous timeout - requires reconciliation.</summary>
    Unknown = 7
}
