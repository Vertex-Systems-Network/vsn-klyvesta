using Klyvesta.Domain.Entities;

namespace Klyvesta.Domain.Services;

/// <summary>
/// Compliance gate interface for regulatory checks.
/// Enforces mandate requirements and eligibility rules.
/// </summary>
public interface IComplianceGate
{
    /// <summary>
    /// Check if an order intent passes all compliance checks.
    /// Returns denial reason if rejected, null if approved.
    /// </summary>
    Task<ComplianceCheckResult> CheckAsync(OrderIntent order, CustomerComplianceStatus status, CancellationToken ct);
}

/// <summary>
/// Customer compliance status for gate checks.
/// </summary>
public sealed class CustomerComplianceStatus
{
    public Guid CustomerId { get; }
    public bool HasValidMandate { get; }
    public DateTime? MandateExpiryUtc { get; }
    public bool IsEligibleForRequestedOperation { get; }
    public string? RestrictedSymbols { get; } // Comma-separated list if any
    public bool IsOnHold { get; }
    public string? HoldReason { get; }
    
    public CustomerComplianceStatus(
        Guid customerId,
        bool hasValidMandate,
        DateTime? mandateExpiryUtc,
        bool isEligibleForRequestedOperation,
        string? restrictedSymbols = null,
        bool isOnHold = false,
        string? holdReason = null)
    {
        CustomerId = customerId;
        HasValidMandate = hasValidMandate;
        MandateExpiryUtc = mandateExpiryUtc;
        IsEligibleForRequestedOperation = isEligibleForRequestedOperation;
        RestrictedSymbols = restrictedSymbols;
        IsOnHold = isOnHold;
        HoldReason = holdReason;
    }
}

/// <summary>
/// Result of a compliance check.
/// </summary>
public sealed class ComplianceCheckResult
{
    public bool Approved { get; }
    public string? DenialReason { get; }
    
    private ComplianceCheckResult(bool approved, string? denialReason)
    {
        Approved = approved;
        DenialReason = denialReason;
    }
    
    public static ComplianceCheckResult Pass() => new(true, null);
    public static ComplianceCheckResult Denied(string reason) => new(false, reason);
}
