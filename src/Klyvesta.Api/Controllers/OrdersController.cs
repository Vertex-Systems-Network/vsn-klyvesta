using Microsoft.AspNetCore.Mvc;
using Klyvesta.Application.Commands;
using Klyvesta.Application.Handlers;
using Klyvesta.Application.Validators;
using Klyvesta.Domain.Services;
using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Api.Controllers;

/// <summary>
/// Order management API endpoints.
/// Paper-mode only - no live trading in P1.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly SubmitOrderIntentHandler _submitHandler;
    private readonly OrderIntentValidator _validator;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        SubmitOrderIntentHandler submitHandler,
        OrderIntentValidator validator,
        ILogger<OrdersController> logger)
    {
        _submitHandler = submitHandler;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Submit a new order intent.
    /// POST /api/orders
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> SubmitOrder([FromBody] SubmitOrderRequest request, CancellationToken ct)
    {
        try
        {
            // Map request to command
            var command = new SubmitOrderIntentCommand(
                request.CustomerId,
                new AccountId(request.AccountId),
                new Symbol(request.Symbol),
                request.Side,
                request.Type,
                new Quantity(request.Quantity),
                request.LimitPrice.HasValue ? new Money(request.LimitPrice.Value, "PKR") : null,
                request.TimeInForce,
                request.IdempotencyKey,
                request.IsAutoOrder
            );

            // Get customer profiles (would come from repository in real implementation)
            var riskProfile = GetDefaultRiskProfile(request.CustomerId);
            var complianceStatus = GetDefaultComplianceStatus(request.CustomerId);

            // Validate
            var validationResult = await _validator.ValidateAsync(command, riskProfile, complianceStatus, ct);
            
            // Handle
            var result = await _submitHandler.HandleAsync(command, validationResult, ct);

            if (result.IsDuplicate)
            {
                return Ok(OrderResponse.FromExisting(result.OrderIntent!, result.BrokerOrderId));
            }

            if (!result.Success)
            {
                if (result.IsUnknown)
                    return StatusCode(504, OrderResponse.Error("Gateway Timeout", "Order outcome unknown - reconciliation required"));

                return BadRequest(OrderResponse.Error(result.Error ?? "Order rejected", result.RejectionReason?.ToString()));
            }

            return Ok(OrderResponse.FromResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting order for customer {CustomerId}", request.CustomerId);
            return StatusCode(500, OrderResponse.Error("Internal server error", ex.Message));
        }
    }

    /// <summary>
    /// Get order by ID.
    /// GET /api/orders/{orderId}
    /// </summary>
    [HttpGet("{orderId:guid}")]
    public ActionResult<OrderResponse> GetOrder(Guid orderId)
    {
        // Would query repository in real implementation
        return Ok(OrderResponse.Error("Not implemented in P1 paper mode", "ORDER_NOT_FOUND"));
    }

    /// <summary>
    /// Cancel an order.
    /// DELETE /api/orders/{orderId}
    /// </summary>
    [HttpDelete("{orderId:guid}")]
    public ActionResult<OrderResponse> CancelOrder(Guid orderId)
    {
        // Would call cancel handler in real implementation
        return Ok(OrderResponse.Error("Not implemented in P1 paper mode", "CANCEL_NOT_IMPLEMENTED"));
    }

    private static CustomerRiskProfile GetDefaultRiskProfile(Guid customerId)
    {
        return new CustomerRiskProfile(
            customerId,
            new Money(1_000_000, "PKR"),  // Max order value
            new Money(5_000_000, "PKR"),  // Max daily turnover
            25m,                          // Max 25% concentration
            new Money(10_000_000, "PKR"), // Max total exposure
            true                          // Eligible for auto trading
        );
    }

    private static CustomerComplianceStatus GetDefaultComplianceStatus(Guid customerId)
    {
        return new CustomerComplianceStatus(
            customerId,
            true,   // Has valid mandate
            DateTime.UtcNow.AddDays(365), // Mandate expires in 1 year
            true,   // Eligible for requested operation
            null,   // No restricted symbols
            false,  // Not on hold
            null    // No hold reason
        );
    }
}

/// <summary>
/// Request model for submitting an order.
/// </summary>
public sealed class SubmitOrderRequest
{
    public Guid CustomerId { get; set; }
    public Guid AccountId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public Domain.Enums.OrderSide Side { get; set; }
    public Domain.Enums.OrderType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal? LimitPrice { get; set; }
    public Domain.Enums.TimeInForce TimeInForce { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool IsAutoOrder { get; set; }
}

/// <summary>
/// Response model for order operations.
/// </summary>
public sealed class OrderResponse
{
    public bool Success { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? BrokerOrderId { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public decimal? FilledQuantity { get; set; }
    public decimal? RemainingQuantity { get; set; }
    public DateTime? CreatedAt { get; set; }

    public static OrderResponse FromResult(SubmitOrderIntentResult result)
    {
        return new OrderResponse
        {
            Success = true,
            OrderId = result.OrderIntent!.Id,
            BrokerOrderId = result.BrokerOrderId,
            Status = result.OrderIntent.Status.ToString(),
            FilledQuantity = (double?)result.OrderIntent.FilledQuantity.Amount,
            RemainingQuantity = (double?)result.OrderIntent.RemainingQuantity.Amount,
            CreatedAt = result.OrderIntent.CreatedAtUtc
        };
    }

    public static OrderResponse FromExisting(Domain.Entities.OrderIntent order, Guid? brokerOrderId)
    {
        return new OrderResponse
        {
            Success = true,
            OrderId = order.Id,
            BrokerOrderId = brokerOrderId,
            Status = order.Status.ToString(),
            FilledQuantity = (double?)order.FilledQuantity.Amount,
            RemainingQuantity = (double?)order.RemainingQuantity.Amount,
            CreatedAt = order.CreatedAtUtc
        };
    }

    public static OrderResponse Error(string message, string? errorCode)
    {
        return new OrderResponse
        {
            Success = false,
            Error = message,
            ErrorCode = errorCode
        };
    }
}
