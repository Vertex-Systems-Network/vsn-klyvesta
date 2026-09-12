namespace Klyvesta.Domain.Portfolio;

using Klyvesta.Domain.Common;

/// <summary>
/// Portfolio holding in a specific instrument.
/// This is a projection from ledger + executions, not authoritative source of truth.
/// </summary>
public sealed class Position : IEntity
{
    public PositionId Id { get; init; } = PositionId.New();
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    
    public required AccountId AccountId { get; init; }
    public required InstrumentId InstrumentId { get; init; }
    public required string Symbol { get; init; }
    
    /// <summary>
    /// Current quantity held (settled + unsettled).
    /// </summary>
    public decimal Quantity { get; private set; }
    
    /// <summary>
    /// Quantity that has settled (T+2 for equities).
    /// </summary>
    public decimal SettledQuantity { get; private set; }
    
    /// <summary>
    /// Quantity pending settlement from recent trades.
    /// </summary>
    public decimal UnsettledQuantity { get; private set; }
    
    /// <summary>
    /// Average cost basis per share.
    /// </summary>
    public Money? AverageCostBasis { get; private set; }
    
    /// <summary>
    /// Total cost basis (Quantity * AverageCostBasis).
    /// </summary>
    public Money? TotalCostBasis { get; private set; }
    
    /// <summary>
    /// Current market value based on latest price.
    /// </summary>
    public Money? CurrentMarketValue { get; private set; }
    
    /// <summary>
    /// Unrealized gain/loss.
    /// </summary>
    public Money? UnrealizedPnL { get; private set; }
    
    /// <summary>
    /// Unrealized gain/loss as percentage.
    /// </summary>
    public Percentage? UnrealizedPnLPercent { get; private set; }
    
    /// <summary>
    /// Last known market price.
    /// </summary>
    public Price? LastPrice { get; private set; }
    public DateTime? LastPriceTimestampUtc { get; private set; }
    
    /// <summary>
    /// Sector classification for concentration checks.
    /// </summary>
    public SectorClassification? Sector { get; init; }
    
    /// <summary>
    /// Industry classification for concentration checks.
    /// </summary>
    public IndustryClassification? Industry { get; init; }
    
    public bool IsLong => Quantity > 0;
    public bool IsFlat => Quantity == 0;
    
    /// <summary>
    /// Updates position after a fill.
    /// </summary>
    public void ApplyFill(decimal quantity, Side side, decimal price, string currency, DateTime fillTime)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Fill quantity must be positive");
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Fill price must be positive");
        
        var fillMoney = new Money(price * quantity, currency);
        
        if (side == Side.Buy)
        {
            // Calculate new average cost basis
            var previousTotalCost = TotalCostBasis?.Amount ?? 0m;
            var newShares = quantity;
            var existingShares = Quantity;
            
            var totalCost = previousTotalCost + fillMoney.Amount;
            var totalShares = existingShares + newShares;
            
            Quantity = totalShares;
            AverageCostBasis = totalShares > 0 ? new Money(totalCost / totalShares, currency) : null;
            TotalCostBasis = new Money(totalCost, currency);
        }
        else // Sell
        {
            if (quantity > Quantity)
                throw new InvalidOperationException($"Cannot sell {quantity} shares, only {Quantity} held");
            
            // Reduce position, cost basis remains same per share
            Quantity -= quantity;
            
            if (Quantity == 0)
            {
                // Position closed - reset cost basis
                AverageCostBasis = null;
                TotalCostBasis = Money.Zero(currency);
            }
            else
            {
                // Recalculate total cost basis for remaining shares
                TotalCostBasis = AverageCostBasis.HasValue 
                    ? new Money(AverageCostBasis.Value.Amount * Quantity, currency) 
                    : null;
            }
        }
        
        // Mark as unsettled initially
        UnsettledQuantity += quantity;
    }
    
    /// <summary>
    /// Marks shares as settled after settlement date.
    /// </summary>
    public void MarkSettled(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Settlement quantity must be positive");
        if (quantity > UnsettledQuantity)
            throw new InvalidOperationException($"Cannot settle {quantity}, only {UnsettledQuantity} unsettled");
        
        UnsettledQuantity -= quantity;
        SettledQuantity += quantity;
    }
    
    /// <summary>
    /// Updates market value based on current price.
    /// </summary>
    public void UpdateMarketPrice(Price price, DateTime priceTimestamp)
    {
        LastPrice = price;
        LastPriceTimestampUtc = priceTimestamp;
        
        if (Quantity > 0)
        {
            CurrentMarketValue = price.ToMoney(new Quantity(Quantity));
            
            if (TotalCostBasis.HasValue)
            {
                var pnl = CurrentMarketValue.Value - TotalCostBasis.Value;
                UnrealizedPnL = pnl;
                
                if (TotalCostBasis.Value.Amount != 0)
                {
                    UnrealizedPnLPercent = Percentage.FromFraction(pnl.Amount / TotalCostBasis.Value.Amount);
                }
            }
        }
        else
        {
            CurrentMarketValue = Money.Zero(price.Currency);
            UnrealizedPnL = Money.Zero(price.Currency);
            UnrealizedPnLPercent = Percentage.Zero;
        }
    }
}

/// <summary>
/// Cash balance for an account, potentially across multiple currencies.
/// </summary>
public sealed class CashAccount : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    
    public required AccountId AccountId { get; init; }
    public required string Currency { get; init; }
    
    /// <summary>
    /// Total cash balance.
    /// </summary>
    public decimal Total { get; private set; }
    
    /// <summary>
    /// Cash available for new orders (not reserved).
    /// </summary>
    public decimal Available { get; private set; }
    
    /// <summary>
    /// Cash reserved for open buy orders.
    /// </summary>
    public decimal Reserved { get; private set; }
    
    /// <summary>
    /// Cash pending settlement from sells.
    /// </summary>
    public decimal Unsettled { get; private set; }
    
    public DateTime LastUpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    
    public Money Balance => new(Total, Currency);
    public Money AvailableBalance => new(Available, Currency);
    public Money ReservedBalance => new(Reserved, Currency);
    public Money UnsettledBalance => new(Unsettled, Currency);
    
    /// <summary>
    /// Reserves cash for a buy order.
    /// </summary>
    public void Reserve(Money amount)
    {
        if (amount.Currency != Currency)
            throw new ArgumentException($"Currency mismatch: expected {Currency}, got {amount.Currency}");
        if (amount.Amount > Available)
            throw new InsufficientCashException(
                $"Insufficient available cash. Required: {amount.Amount}, Available: {Available}",
                amount.Amount,
                Available,
                Currency);
        
        Available -= amount.Amount;
        Reserved += amount.Amount;
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Releases reserved cash (e.g., order cancelled or partially filled).
    /// </summary>
    public void ReleaseReservation(Money amount)
    {
        if (amount.Currency != Currency)
            throw new ArgumentException($"Currency mismatch: expected {Currency}, got {amount.Currency}");
        if (amount.Amount > Reserved)
            throw new InvalidOperationException($"Cannot release {amount.Amount}, only {Reserved} reserved");
        
        Reserved -= amount.Amount;
        Available += amount.Amount;
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Applies a fill against reserved cash.
    /// </summary>
    public void ApplyFillDebit(Money amount)
    {
        if (amount.Currency != Currency)
            throw new ArgumentException($"Currency mismatch: expected {Currency}, got {amount.Currency}");
        if (amount.Amount > Reserved)
            throw new InvalidOperationException($"Fill amount {amount.Amount} exceeds reserved {Reserved}");
        
        Reserved -= amount.Amount;
        Total -= amount.Amount;
        Available = Math.Max(0, Available); // Should already be correct but safety check
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Credits cash from a sell.
    /// </summary>
    public void ApplySellCredit(Money amount, bool markAsUnsettled = true)
    {
        if (amount.Currency != Currency)
            throw new ArgumentException($"Currency mismatch: expected {Currency}, got {amount.Currency}");
        
        Total += amount.Amount;
        
        if (markAsUnsettled)
        {
            Unsettled += amount.Amount;
        }
        else
        {
            Available += amount.Amount;
        }
        
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Marks unsettled cash as settled.
    /// </summary>
    public void MarkSettled(Money amount)
    {
        if (amount.Currency != Currency)
            throw new ArgumentException($"Currency mismatch: expected {Currency}, got {amount.Currency}");
        if (amount.Amount > Unsettled)
            throw new InvalidOperationException($"Cannot settle {amount.Amount}, only {Unsettled} unsettled");
        
        Unsettled -= amount.Amount;
        Available += amount.Amount;
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Deposits cash into the account.
    /// </summary>
    public void Deposit(Money amount)
    {
        if (amount.Currency != Currency)
            throw new ArgumentException($"Currency mismatch: expected {Currency}, got {amount.Currency}");
        
        Total += amount.Amount;
        Available += amount.Amount;
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Withdraws cash from the account.
    /// </summary>
    public void Withdraw(Money amount)
    {
        if (amount.Currency != Currency)
            throw new ArgumentException($"Currency mismatch: expected {Currency}, got {amount.Currency}");
        if (amount.Amount > Available)
            throw new InsufficientCashException(
                $"Insufficient available cash for withdrawal. Required: {amount.Amount}, Available: {Available}",
                amount.Amount,
                Available,
                Currency);
        
        Total -= amount.Amount;
        Available -= amount.Amount;
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Snapshot of entire portfolio for risk evaluation.
/// </summary>
public readonly record struct PortfolioSnapshot(
    AccountId AccountId,
    DateTime AsOfUtc,
    Money TotalValue,
    Money CashValue,
    Money SecuritiesValue,
    IReadOnlyList<PositionSnapshot> Positions,
    Percentage CashPercent,
    IReadOnlyList<SectorAllocation> SectorAllocations,
    IReadOnlyList<IndustryAllocation> IndustryAllocations,
    decimal TurnoverToday,
    int OrdersToday
);

/// <summary>
/// Sector allocation in portfolio.
/// </summary>
public readonly record struct SectorAllocation(
    string SectorCode,
    string SectorName,
    Money Value,
    Percentage Weight
);

/// <summary>
/// Industry allocation in portfolio.
/// </summary>
public readonly record struct IndustryAllocation(
    string IndustryCode,
    string IndustryName,
    Money Value,
    Percentage Weight
);

/// <summary>
/// Exception thrown when cash operations fail due to insufficient funds.
/// </summary>
public sealed class InsufficientCashException : Exception
{
    public InsufficientCashException(string message, decimal Required, decimal Available, string Currency)
        : base(message)
    {
        this.Required = Required;
        this.Available = Available;
        this.Currency = Currency;
    }
    
    public decimal Required { get; }
    public decimal Available { get; }
    public string Currency { get; }
}

/// <summary>
/// Service interface for portfolio management.
/// </summary>
public interface IPortfolioService
{
    /// <summary>
    /// Gets current position for an account/instrument.
    /// </summary>
    Task<Position?> GetPositionAsync(AccountId accountId, InstrumentId instrumentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all positions for an account.
    /// </summary>
    Task<IReadOnlyList<Position>> GetPositionsAsync(AccountId accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets cash account for a specific currency.
    /// </summary>
    Task<CashAccount?> GetCashAccountAsync(AccountId accountId, string currency, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all cash accounts for an account.
    /// </summary>
    Task<IReadOnlyList<CashAccount>> GetCashAccountsAsync(AccountId accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets full portfolio snapshot for risk evaluation.
    /// </summary>
    Task<PortfolioSnapshot> GetPortfolioSnapshotAsync(AccountId accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Projects portfolio state after a proposed trade (without executing).
    /// </summary>
    Task<PortfolioSnapshot> ProjectPortfolioAfterTradeAsync(
        AccountId accountId,
        Side side,
        InstrumentId instrumentId,
        decimal quantity,
        decimal? price,
        CancellationToken cancellationToken = default);
}
