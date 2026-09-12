namespace Klyvesta.Domain.Enums;

/// <summary>
/// Time-in-force specification for order validity.
/// </summary>
public enum TimeInForce
{
    /// <summary>Valid for current trading day only.</summary>
    Day = 1,
    
    /// <summary>Good until cancelled.</summary>
    GTC = 2,
    
    /// <summary>Immediate or cancel - fill what's possible immediately.</summary>
    IOC = 3,
    
    /// <summary>Fill or kill - must fill entirely immediately.</summary>
    FOK = 4
}
