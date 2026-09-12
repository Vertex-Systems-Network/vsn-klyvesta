using Klyvesta.Domain.Common;
using Klyvesta.Domain.Orders;
using Klyvesta.Domain.Persistence;
using Klyvesta.Domain.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klyvesta.Domain.Services;

/// <summary>
/// Service implementation for OrderIntent lifecycle management.
/// Enforces state machine transitions, idempotency, and audit trails.
/// </summary>
public class OrderIntentService : IOrderIntentService
{
    private readonly KlyvestaDbContext _dbContext;
    private readonly ILogger<OrderIntentService> _logger;

    public OrderIntentService(
        KlyvestaDbContext dbContext,
        ILogger<OrderIntentService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OrderIntent> CreateOrderIntentAsync(
        CustomerId customerId,
        AccountId accountId,
        Symbol symbol,
        Side side,
        Quantity quantity,
        Price? limitPrice,
        TimeInForce timeInForce,
        string? clientOrderId,
        AiProposalId? aiProposalId,
        MandateId? mandateId,
        string? idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        // Validate idempotency key if provided
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await _dbContext.OrderIntents
                .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, cancellationToken);
            
            if (existing != null)
            {
                _logger.LogInformation("Returning existing OrderIntent for idempotency key {IdempotencyKey}", idempotencyKey);
                return MapToDomain(existing);
            }
        }

        var orderIntent = OrderIntent.Create(
            customerId,
            accountId,
            symbol,
            side,
            quantity,
            limitPrice,
            timeInForce,
            clientOrderId,
            aiProposalId,
            mandateId);

        var entity = MapToEntity(orderIntent);
        entity.IdempotencyKey = idempotencyKey;
        entity.CorrelationId = correlationId;

        await _dbContext.OrderIntents.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created OrderIntent {OrderIntentId} for customer {CustomerId}", 
            orderIntent.Id, customerId);

        return orderIntent;
    }

    public async Task<OrderIntent> SubmitOrderAsync(
        OrderIntentId orderIntentId,
        string submittedBy,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrderIntentEntityAsync(orderIntentId, cancellationToken);
        
        var orderIntent = MapToDomain(entity);
        var submitted = orderIntent.Submit(submittedBy);

        UpdateEntityFromDomain(entity, submitted);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Submitted OrderIntent {OrderIntentId}", orderIntentId);
        return submitted;
    }

    public async Task<OrderIntent> AcknowledgeOrderAsync(
        OrderIntentId orderIntentId,
        string brokerOrderId,
        string acknowledgedBy,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrderIntentEntityAsync(orderIntentId, cancellationToken);
        
        var orderIntent = MapToDomain(entity);
        var acknowledged = orderIntent.Acknowledge(brokerOrderId, acknowledgedBy);

        UpdateEntityFromDomain(entity, acknowledged);
        entity.BrokerOrderId = brokerOrderId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Acknowledged OrderIntent {OrderIntentId} with broker order {BrokerOrderId}", 
            orderIntentId, brokerOrderId);
        return acknowledged;
    }

    public async Task<OrderIntent> RecordFillAsync(
        OrderIntentId orderIntentId,
        Quantity fillQuantity,
        Price fillPrice,
        DateTime fillTimestamp,
        string? brokerExecutionId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrderIntentEntityAsync(orderIntentId, cancellationToken);
        
        var orderIntent = MapToDomain(entity);
        var filled = orderIntent.RecordFill(fillQuantity, fillPrice, fillTimestamp, brokerExecutionId);

        UpdateEntityFromDomain(entity, filled);
        
        // Record execution
        var execution = new OrderExecutionEntity
        {
            Id = Guid.NewGuid(),
            OrderIntentId = orderIntentId,
            FillQuantity = fillQuantity.Units,
            FillPrice = fillPrice.Units,
            FillValue = (fillQuantity * fillPrice).Units,
            FillTimestampUtc = fillTimestamp,
            BrokerExecutionId = brokerExecutionId,
            SettlementDateUtc = fillTimestamp.AddDays(2), // T+2
            CreatedAtUtc = DateTime.UtcNow
        };
        
        await _dbContext.OrderExecutions.AddAsync(execution, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recorded fill for OrderIntent {OrderIntentId}: {Quantity}@{Price}", 
            orderIntentId, fillQuantity, fillPrice);
        return filled;
    }

    public async Task<OrderIntent> CancelOrderAsync(
        OrderIntentId orderIntentId,
        string cancelledBy,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrderIntentEntityAsync(orderIntentId, cancellationToken);
        
        var orderIntent = MapToDomain(entity);
        var cancelled = orderIntent.Cancel(cancelledBy, reason);

        UpdateEntityFromDomain(entity, cancelled);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cancelled OrderIntent {OrderIntentId}: {Reason}", 
            orderIntentId, reason ?? "No reason provided");
        return cancelled;
    }

    public async Task<OrderIntent> RejectOrderAsync(
        OrderIntentId orderIntentId,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrderIntentEntityAsync(orderIntentId, cancellationToken);
        
        var orderIntent = MapToDomain(entity);
        var rejected = orderIntent.Reject(rejectedBy, reason);

        UpdateEntityFromDomain(entity, rejected);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Rejected OrderIntent {OrderIntentId}: {Reason}", 
            orderIntentId, reason);
        return rejected;
    }

    public async Task<OrderIntent> FailOrderAsync(
        OrderIntentId orderIntentId,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrderIntentEntityAsync(orderIntentId, cancellationToken);
        
        var orderIntent = MapToDomain(entity);
        var failed = orderIntent.Fail(failureReason);

        UpdateEntityFromDomain(entity, failed);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogError("Failed OrderIntent {OrderIntentId}: {Reason}", 
            orderIntentId, failureReason);
        return failed;
    }

    public async Task<OrderIntent?> GetOrderByIdAsync(
        OrderIntentId orderIntentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.OrderIntents
            .FirstOrDefaultAsync(o => o.Id == orderIntentId, cancellationToken);
        
        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<OrderIntent>> GetOrdersByAccountAsync(
        AccountId accountId,
        OrderState? state = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OrderIntents.Where(o => o.AccountId == accountId);
        
        if (state.HasValue)
        {
            query = query.Where(o => o.State == state.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<OrderIntent>> GetOrdersByCustomerAsync(
        CustomerId customerId,
        OrderState? state = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OrderIntents.Where(o => o.CustomerId == customerId);
        
        if (state.HasValue)
        {
            query = query.Where(o => o.State == state.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);
        return entities.Select(MapToDomain).ToList();
    }

    private async Task<OrderIntentEntity> GetOrderIntentEntityAsync(
        OrderIntentId orderIntentId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.OrderIntents
            .FirstOrDefaultAsync(o => o.Id == orderIntentId, cancellationToken);
        
        if (entity == null)
        {
            throw new InvalidOperationException($"OrderIntent {orderIntentId} not found");
        }

        return entity;
    }

    private OrderIntentEntity MapToEntity(OrderIntent orderIntent)
    {
        return new OrderIntentEntity
        {
            Id = orderIntent.Id,
            CustomerId = orderIntent.CustomerId,
            AccountId = orderIntent.AccountId,
            Symbol = orderIntent.Symbol.Value,
            Side = orderIntent.Side,
            Quantity = orderIntent.Quantity.Units,
            Currency = orderIntent.Quantity.Currency,
            LimitPrice = orderIntent.LimitPrice?.Units,
            TimeInForce = orderIntent.TimeInForce,
            State = orderIntent.State,
            ClientOrderId = orderIntent.ClientOrderId,
            AiProposalId = orderIntent.AiProposalId,
            MandateId = orderIntent.MandateId,
            ApprovedBy = orderIntent.ApprovedBy,
            RiskDecisionId = orderIntent.RiskDecisionId,
            ComplianceDecisionId = orderIntent.ComplianceDecisionId,
            BrokerOrderId = orderIntent.BrokerOrderId,
            FillQuantity = orderIntent.FillQuantity?.Units,
            RemainingQuantity = orderIntent.RemainingQuantity?.Units,
            AverageFillPrice = orderIntent.AverageFillPrice?.Units,
            SettledQuantity = orderIntent.SettledQuantity?.Units,
            ExternalTimestampUtc = orderIntent.ExternalTimestampUtc,
            ObservedAtUtc = orderIntent.ObservedAtUtc,
            CreatedAtUtc = orderIntent.CreatedAtUtc,
            UpdatedAtUtc = orderIntent.UpdatedAtUtc
        };
    }

    private OrderIntent MapToDomain(OrderIntentEntity entity)
    {
        var orderIntent = new OrderIntent(
            entity.Id,
            entity.CustomerId,
            entity.AccountId,
            new Symbol(entity.Symbol),
            entity.Side,
            new Quantity(entity.Quantity, entity.Currency),
            entity.LimitPrice.HasValue ? new Price(entity.LimitPrice.Value, entity.Currency) : null,
            entity.TimeInForce,
            entity.ClientOrderId,
            entity.AiProposalId,
            entity.MandateId)
        {
            State = entity.State,
            ApprovedBy = entity.ApprovedBy,
            RiskDecisionId = entity.RiskDecisionId,
            ComplianceDecisionId = entity.ComplianceDecisionId,
            BrokerOrderId = entity.BrokerOrderId,
            ExternalTimestampUtc = entity.ExternalTimestampUtc,
            ObservedAtUtc = entity.ObservedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };

        // Restore fill history if needed (simplified - would need execution history)
        if (entity.FillQuantity.HasValue)
        {
            var fillQty = new Quantity(entity.FillQuantity.Value, entity.Currency);
            var remainingQty = entity.RemainingQuantity.HasValue 
                ? new Quantity(entity.RemainingQuantity.Value, entity.Currency) 
                : fillQty;
            var avgPrice = entity.AverageFillPrice.HasValue 
                ? new Price(entity.AverageFillPrice.Value, entity.Currency) 
                : null;
            var settledQty = entity.SettledQuantity.HasValue 
                ? new Quantity(entity.SettledQuantity.Value, entity.Currency) 
                : null;

            // Note: In a real implementation, we'd reconstruct from OrderExecutionEntity records
            // This is a simplified restoration
        }

        return orderIntent;
    }

    private void UpdateEntityFromDomain(OrderIntentEntity entity, OrderIntent updated)
    {
        entity.State = updated.State;
        entity.ApprovedBy = updated.ApprovedBy;
        entity.RiskDecisionId = updated.RiskDecisionId;
        entity.ComplianceDecisionId = updated.ComplianceDecisionId;
        entity.BrokerOrderId = updated.BrokerOrderId;
        entity.FillQuantity = updated.FillQuantity?.Units;
        entity.RemainingQuantity = updated.RemainingQuantity?.Units;
        entity.AverageFillPrice = updated.AverageFillPrice?.Units;
        entity.SettledQuantity = updated.SettledQuantity?.Units;
        entity.UpdatedAtUtc = DateTime.UtcNow;
    }
}
