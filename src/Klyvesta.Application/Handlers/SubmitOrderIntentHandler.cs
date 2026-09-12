using Klyvesta.Application.Commands;
using Klyvesta.Domain.Entities;
using Klyvesta.Domain.Services;
using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Application.Handlers;

/// <summary>
/// Handles order intent submission commands.
/// Implements idempotency, validation, and broker submission.
/// </summary>
public sealed class SubmitOrderIntentHandler
{
    private readonly IBrokerAdapter _brokerAdapter;
    private readonly IIdempotencyService? _idempotencyService;

    public SubmitOrderIntentHandler(
        IBrokerAdapter brokerAdapter,
        IIdempotencyService? idempotencyService = null)
    {
        _brokerAdapter = brokerAdapter;
        _idempotencyService = idempotencyService;
    }

    /// <summary>
    /// Handle order intent submission.
    /// Returns the created or existing order intent.
    /// </summary>
    public async Task<SubmitOrderIntentResult> HandleAsync(
        SubmitOrderIntentCommand command,
        OrderIntentValidationResult validationResult,
        CancellationToken ct)
    {
        // Check for duplicate command via idempotency
        if (!string.IsNullOrEmpty(command.IdempotencyKey) && _idempotencyService != null)
        {
            var existing = await _idempotencyService.GetExistingResultAsync<OrderIntent>(
                command.IdempotencyKey, ct);
            
            if (existing != null)
                return SubmitOrderIntentResult.Duplicate(existing);
        }

        // Validate first
        if (!validationResult.IsValid)
        {
            return validationResult.IsRiskDenied
                ? SubmitOrderIntentResult.RiskDenied(validationResult.Error!)
                : validationResult.IsComplianceDenied
                    ? SubmitOrderIntentResult.ComplianceDenied(validationResult.Error!)
                    : SubmitOrderIntentResult.Invalid(validationResult.Errors);
        }

        // Create order intent
        var orderIntent = new OrderIntent(
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

        // Submit to broker
        var brokerResult = await _brokerAdapter.SubmitOrderAsync(orderIntent, ct);

        if (brokerResult.Success && brokerResult.OrderId.HasValue)
        {
            orderIntent.Submit();
            
            // Store idempotency record
            if (!string.IsNullOrEmpty(command.IdempotencyKey) && _idempotencyService != null)
            {
                await _idempotencyService.StoreResultAsync(
                    command.IdempotencyKey, orderIntent, ct);
            }

            return SubmitOrderIntentResult.Succeeded(orderIntent, brokerResult.OrderId.Value);
        }
        else if (brokerResult.IsUnknown)
        {
            orderIntent.MarkUnknown();
            return SubmitOrderIntentResult.Unknown(brokerResult.Details);
        }
        else
        {
            var rejectionReason = brokerResult.RejectionReason ?? RejectionReason.BrokerRejected;
            orderIntent.Reject(rejectionReason, brokerResult.Details);
            
            return SubmitOrderIntentResult.Rejected(rejectionReason, brokerResult.Details);
        }
    }
}

/// <summary>
/// Result of handling a submit order intent command.
/// </summary>
public sealed class SubmitOrderIntentResult
{
    public bool Success { get; }
    public OrderIntent? OrderIntent { get; }
    public Guid? BrokerOrderId { get; }
    public string? Error { get; }
    public IReadOnlyList<string>? Errors { get; }
    public bool IsDuplicate { get; }
    public bool IsUnknown { get; }
    public RejectionReason? RejectionReason { get; }

    private SubmitOrderIntentResult(
        bool success,
        OrderIntent? orderIntent,
        Guid? brokerOrderId,
        string? error,
        List<string>? errors,
        bool isDuplicate,
        bool isUnknown,
        RejectionReason? rejectionReason)
    {
        Success = success;
        OrderIntent = orderIntent;
        BrokerOrderId = brokerOrderId;
        Error = error;
        Errors = errors;
        IsDuplicate = isDuplicate;
        IsUnknown = isUnknown;
        RejectionReason = rejectionReason;
    }

    public static SubmitOrderIntentResult Succeeded(OrderIntent orderIntent, Guid brokerOrderId)
        => new(true, orderIntent, brokerOrderId, null, null, false, false, null);

    public static SubmitOrderIntentResult Duplicate(OrderIntent existingOrderIntent)
        => new(true, existingOrderIntent, null, "Duplicate command detected", null, true, false, null);

    public static SubmitOrderIntentResult RiskDenied(string reason)
        => new(false, null, null, reason, null, false, false, RejectionReason.RiskGovernorDenial);

    public static SubmitOrderIntentResult ComplianceDenied(string reason)
        => new(false, null, null, reason, null, false, false, RejectionReason.ComplianceHold);

    public static SubmitOrderIntentResult Invalid(List<string> errors)
        => new(false, null, null, "Validation failed", errors, false, false, RejectionReason.InvalidParameters);

    public static SubmitOrderIntentResult Rejected(RejectionReason reason, string? details)
        => new(false, null, null, details, null, false, false, reason);

    public static SubmitOrderIntentResult Unknown(string? details)
        => new(false, null, null, details, null, false, true, null);
}

/// <summary>
/// Idempotency service interface for duplicate command detection.
/// </summary>
public interface IIdempotencyService
{
    Task<T?> GetExistingResultAsync<T>(string idempotencyKey, CancellationToken ct) where T : class;
    Task StoreResultAsync<T>(string idempotencyKey, T result, CancellationToken ct) where T : class;
}
