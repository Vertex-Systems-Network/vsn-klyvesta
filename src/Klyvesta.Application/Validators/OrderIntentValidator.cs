using Klyvesta.Domain.Entities;
using Klyvesta.Domain.Services;
using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Application.Validators;

/// <summary>
/// Validates order intent commands before processing.
/// Enforces business rules and compliance checks.
/// </summary>
public sealed class OrderIntentValidator
{
    private readonly IRiskGovernor _riskGovernor;
    private readonly IComplianceGate _complianceGate;

    public OrderIntentValidator(IRiskGovernor riskGovernor, IComplianceGate complianceGate)
    {
        _riskGovernor = riskGovernor;
        _complianceGate = complianceGate;
    }

    /// <summary>
    /// Validate an order intent command.
    /// Returns validation result with any errors.
    /// </summary>
    public async Task<OrderIntentValidationResult> ValidateAsync(
        SubmitOrderIntentCommand command,
        CustomerRiskProfile riskProfile,
        CustomerComplianceStatus complianceStatus,
        CancellationToken ct)
    {
        var errors = new List<string>();

        // Basic validation
        if (command.Quantity <= Quantity.Zero)
            errors.Add("Quantity must be greater than zero");

        if (command.Type == OrderType.Limit && command.LimitPrice == null)
            errors.Add("Limit price is required for limit orders");

        if (command.Type == OrderType.Limit && command.LimitPrice!.Amount < 0)
            errors.Add("Limit price cannot be negative");

        if (errors.Count > 0)
            return OrderIntentValidationResult.Invalid(errors);

        // Risk governor check
        var orderIntent = CreateTemporaryOrderIntent(command);
        var riskResult = await _riskGovernor.CheckAsync(orderIntent, riskProfile, ct);
        if (!riskResult.Approved)
            return OrderIntentValidationResult.RiskDenied(riskResult.DenialReason!);

        // Compliance gate check
        var complianceResult = await _complianceGate.CheckAsync(orderIntent, complianceStatus, ct);
        if (!complianceResult.Approved)
            return OrderIntentValidationResult.ComplianceDenied(complianceResult.DenialReason!);

        return OrderIntentValidationResult.Valid();
    }

    private static OrderIntent CreateTemporaryOrderIntent(SubmitOrderIntentCommand command)
    {
        return new OrderIntent(
            Guid.CreateVersion7(),
            command.CustomerId,
            command.Symbol.Value,
            command.Side,
            command.Type,
            command.Quantity,
            command.LimitPrice,
            command.TimeInForce,
            command.IdempotencyKey
        );
    }
}

/// <summary>
/// Result of order intent validation.
/// </summary>
public sealed class OrderIntentValidationResult
{
    public bool IsValid { get; }
    public bool IsRiskDenied { get; }
    public bool IsComplianceDenied { get; }
    public string? Error { get; }
    public IReadOnlyList<string> Errors { get; }

    private OrderIntentValidationResult(
        bool isValid,
        bool isRiskDenied,
        bool isComplianceDenied,
        string? error,
        List<string>? errors)
    {
        IsValid = isValid;
        IsRiskDenied = isRiskDenied;
        IsComplianceDenied = isComplianceDenied;
        Error = error;
        Errors = errors ?? Array.Empty<string>();
    }

    public static OrderIntentValidationResult Valid()
        => new(true, false, false, null, null);

    public static OrderIntentValidationResult Invalid(List<string> errors)
        => new(false, false, false, "Validation failed", errors);

    public static OrderIntentValidationResult RiskDenied(string reason)
        => new(false, true, false, reason, null);

    public static OrderIntentValidationResult ComplianceDenied(string reason)
        => new(false, false, true, reason, null);
}
