using Klyvesta.Domain.Entities;
using Klyvesta.Domain.Services;
using Klyvesta.Domain.ValueObjects;
using Klyvesta.Infrastructure.Persistence;
using Klyvesta.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klyvesta.Infrastructure.Services;

/// <summary>
/// Implementation of position tracking service.
/// Maintains projected holdings based on executions.
/// </summary>
public sealed class PositionService : IPositionService
{
    private readonly KlyvestaDbContext _dbContext;
    private readonly ILogger<PositionService> _logger;

    public PositionService(KlyvestaDbContext dbContext, ILogger<PositionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Position> GetPositionAsync(Guid customerId, string symbol, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var record = await _dbContext.Positions
            .FirstOrDefaultAsync(p => p.CustomerId == customerId && p.Symbol == symbol, ct);

        if (record == null)
        {
            // Return zero position
            return new Position(
                customerId,
                new Symbol(symbol),
                Quantity.Zero(),
                Money.Zero("PKR"),
                DateTime.UtcNow);
        }

        return new Position(
            record.CustomerId,
            new Symbol(record.Symbol),
            new Quantity(record.Quantity),
            new Money(record.AverageCostBasis, record.Currency),
            record.LastUpdatedAtUtc);
    }

    public async Task<IReadOnlyList<Position>> GetAllPositionsAsync(Guid customerId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var records = await _dbContext.Positions
            .Where(p => p.CustomerId == customerId && p.Quantity > 0)
            .ToListAsync(ct);

        return records.Select(r => new Position(
            r.CustomerId,
            new Symbol(r.Symbol),
            new Quantity(r.Quantity),
            new Money(r.AverageCostBasis, r.Currency),
            r.LastUpdatedAtUtc)).ToList().AsReadOnly();
    }

    public async Task ApplyExecutionAsync(Execution execution, OrderSide side, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            // Find or create position record
            var record = await _dbContext.Positions
                .FirstOrDefaultAsync(p => 
                    p.CustomerId == execution.CustomerId && 
                    p.Symbol == execution.Symbol.Value, ct);

            if (record == null)
            {
                // Create new position
                record = new PositionRecord
                {
                    Id = Guid.CreateVersion7(),
                    CustomerId = execution.CustomerId,
                    Symbol = execution.Symbol.Value,
                    Currency = execution.Currency.Value,
                    Quantity = 0,
                    AverageCostBasis = 0,
                    LastUpdatedAtUtc = DateTime.UtcNow
                };
                _dbContext.Positions.Add(record);
            }

            // Calculate new position based on side
            var executionQty = execution.Quantity.Amount;
            var executionValue = executionQty * execution.PricePerUnit.Amount;

            if (side == OrderSide.Buy)
            {
                // Buy: increase quantity, update average cost basis
                var currentQty = record.Quantity;
                var currentCostBasis = record.AverageCostBasis * currentQty;
                
                var newQty = currentQty + executionQty;
                var newTotalCost = currentCostBasis + executionValue;
                var newAvgCostBasis = newQty > 0 ? newTotalCost / newQty : 0;

                record.Quantity = newQty;
                record.AverageCostBasis = newAvgCostBasis;
            }
            else // Sell
            {
                // Sell: decrease quantity, cost basis remains same per unit
                var newQty = record.Quantity - executionQty;
                
                if (newQty < 0)
                {
                    _logger.LogError(
                        "Execution {ExecutionId} would result in negative position for customer {CustomerId} symbol {Symbol}",
                        execution.Id, execution.CustomerId, execution.Symbol.Value);
                    throw new InvalidOperationException(
                        $"Execution would result in negative position: {newQty}");
                }

                record.Quantity = newQty;
                // Average cost basis per unit remains unchanged on sells
            }

            record.LastUpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Applied execution {ExecutionId} to position for customer {CustomerId} symbol {Symbol}. New quantity: {Quantity}",
                execution.Id, execution.CustomerId, execution.Symbol.Value, record.Quantity);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex,
                "Failed to apply execution {ExecutionId} to position",
                execution.Id);
            throw;
        }
    }
}
