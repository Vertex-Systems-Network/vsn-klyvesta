using Klyvesta.Domain.Entities;
using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Domain.Services;

/// <summary>
/// Risk governor interface for deterministic risk limits.
/// AI cannot override these denials.
/// </summary>
public interface IRiskGovernor
{
    /// <summary>
    /// Check if an order intent passes all risk checks.
    /// Returns denial reason if rejected, null if approved.
    /// </summary>
    Task<RiskCheckResult> CheckAsync(OrderIntent order, CustomerRiskProfile profile, CancellationToken ct);
}

/// <summary>
/// Customer risk profile for governor checks.
/// </summary>
public sealed class CustomerRiskProfile
{
    public Guid CustomerId { get; }
    public Money MaxOrderValue { get; }
    public Money MaxDailyTurnover { get; }
    public decimal MaxConcentrationPercent { get; } // 0-100
    public Money MaxTotalExposure { get; }
    public bool IsEligibleForAutoTrading { get; }
    
    public CustomerRiskProfile(
        Guid customerId,
        Money maxOrderValue,
        Money maxDailyTurnover,
        decimal maxConcentrationPercent,
        Money maxTotalExposure,
        bool isEligibleForAutoTrading)
    {
        if (maxConcentrationPercent < 0 || maxConcentrationPercent > 100)
            throw new ArgumentException("Concentration must be 0-100", nameof(maxConcentrationPercent));
        
        CustomerId = customerId;
        MaxOrderValue = maxOrderValue;
        MaxDailyTurnover = maxDailyTurnover;
        MaxConcentrationPercent = maxConcentrationPercent;
        MaxTotalExposure = maxTotalExposure;
        IsEligibleForAutoTrading = isEligibleForAutoTrading;
    }
}

/// <summary>
/// Result of a risk check.
/// </summary>
public sealed class RiskCheckResult
{
    public bool Approved { get; }
    public string? DenialReason { get; }
    
    private RiskCheckResult(bool approved, string? denialReason)
    {
        Approved = approved;
        DenialReason = denialReason;
    }
    
    public static RiskCheckResult Pass() => new(true, null);
    public static RiskCheckResult Denied(string reason) => new(false, reason);
}
