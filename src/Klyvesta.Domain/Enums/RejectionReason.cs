namespace Klyvesta.Domain.Enums;

/// <summary>
/// Enumerated rejection reasons for audit and user feedback.
/// </summary>
public enum RejectionReason
{
    /// <summary>No specific reason provided.</summary>
    None = 0,
    
    /// <summary>Insufficient cash or buying power.</summary>
    InsufficientFunds = 1,
    
    /// <summary>Insufficient position quantity to sell.</summary>
    InsufficientPosition = 2,
    
    /// <summary>Market is closed.</summary>
    MarketClosed = 3,
    
    /// <summary>Instrument not allowed for this customer/package.</summary>
    InstrumentNotAllowed = 4,
    
    /// <summary>Order size exceeds limits.</summary>
    OrderSizeExceeded = 5,
    
    /// <summary>Risk governor denied the order.</summary>
    RiskGovernorDenial = 6,
    
    /// <summary>Compliance gate blocked the order.</summary>
    ComplianceHold = 7,
    
    /// <summary>No valid mandate for auto execution.</summary>
    NoMandate = 8,
    
    /// <summary>Stale market data - cannot price accurately.</summary>
    StaleMarketData = 9,
    
    /// <summary>Broker unavailable.</summary>
    BrokerUnavailable = 10,
    
    /// <summary>Invalid order parameters.</summary>
    InvalidParameters = 11,
    
    /// <summary>Duplicate order detected.</summary>
    DuplicateOrder = 12,
    
    /// <summary>Unauthorized access to resource.</summary>
    Unauthorized = 13
}
