using Klyvesta.Domain.Entities;
using Klyvesta.Domain.Enums;
using Klyvesta.Domain.Services;
using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Infrastructure.Adapters;

/// <summary>
/// Paper broker adapter that simulates broker behavior for testing.
/// Implements all 20 paper broker scenarios from PAPER_BROKER_SCENARIOS_V1.yaml.
/// </summary>
public sealed class PaperBrokerAdapter : IBrokerAdapter
{
    private readonly PaperBrokerConfig _config;
    private readonly ILogger<PaperBrokerAdapter>? _logger;

    // In-memory state for simulation
    private readonly Dictionary<Guid, BrokerOrder> _orders = new();
    private readonly Dictionary<Guid, Money> _cashBalances = new();
    private readonly Dictionary<(Guid CustomerId, string Symbol), Quantity> _positions = new();
    private readonly HashSet<string> _processedIdempotencyKeys = new();

    public PaperBrokerAdapter(PaperBrokerConfig config, ILogger<PaperBrokerAdapter>? logger = null)
    {
        _config = config;
        _logger = logger;
        
        // Initialize with default cash balance for testing
        _cashBalances[Guid.Empty] = new Money(1_000_000, "PKR"); // Default test customer
    }

    public Task<BrokerSubmitResult> SubmitOrderAsync(OrderIntent order, CancellationToken ct)
    {
        // Check idempotency
        if (!string.IsNullOrEmpty(order.IdempotencyKey))
        {
            if (_processedIdempotencyKeys.Contains(order.IdempotencyKey))
            {
                // Return existing order if duplicate
                var existingOrder = _orders.Values.FirstOrDefault(o => o.IdempotencyKey == order.IdempotencyKey);
                if (existingOrder != null)
                    return Task.FromResult(BrokerSubmitResult.Succeeded(existingOrder.BrokerOrderId));
            }
            _processedIdempotencyKeys.Add(order.IdempotencyKey);
        }

        // Check market hours
        if (!_config.MarketOpen && !_config.AllowExecutionWhenMarketClosed)
        {
            return Task.FromResult(BrokerSubmitResult.Rejected(
                RejectionReason.MarketClosed, 
                "Market is closed"));
        }

        // Check health
        if (!_config.IsHealthy && !_config.AllowExecutionWhenUnavailable)
        {
            return Task.FromResult(BrokerSubmitResult.Failed("Broker unavailable"));
        }

        // Simulate scenario-based behavior
        var result = SimulateScenario(order);
        return Task.FromResult(result);
    }

    private BrokerSubmitResult SimulateScenario(OrderIntent order)
    {
        var orderId = Guid.CreateVersion7();
        
        // Check for specific test scenarios
        if (_config.ScenarioId != null)
        {
            return _config.ScenarioId switch
            {
                "PB-002" => BrokerSubmitResult.Rejected(RejectionReason.InsufficientFunds, "Insufficient buying power"),
                "PB-006" or "PB-007" => BrokerSubmitResult.Unknown("Timeout - outcome ambiguous"),
                "PB-014" => BrokerSubmitResult.Failed("Broker connection failed"),
                "PB-017" => BrokerSubmitResult.Rejected(RejectionReason.Unauthorized, "Unauthorized access"),
                "PB-018" => BrokerSubmitResult.Rejected(RejectionReason.NoMandate, "No valid mandate for auto trading"),
                "PB-019" => BrokerSubmitResult.Rejected(RejectionReason.RiskGovernorDenial, "Risk limit breached"),
                _ => CreateSuccessResult(order, orderId)
            };
        }

        // Default: success with immediate fill for market orders
        return CreateSuccessResult(order, orderId);
    }

    private BrokerSubmitResult CreateSuccessResult(OrderIntent order, Guid orderId)
    {
        // Create broker order record
        var brokerOrder = new BrokerOrder(
            orderId,
            order.Id,
            order.CustomerId,
            order.Symbol,
            order.Side,
            order.Type,
            order.Quantity,
            order.LimitPrice,
            order.TimeInForce,
            order.IdempotencyKey
        );
        
        brokerOrder.SubmitToBroker();
        _orders[orderId] = brokerOrder;

        // For market orders in paper mode, simulate immediate full fill
        if (order.Type == OrderType.Market && _config.AutoFillMarketOrders)
        {
            var execution = new Execution(
                Guid.CreateVersion7(),
                orderId,
                order.Quantity,
                order.LimitPrice ?? new Money(100, "PKR"), // Default price for simulation
                DateTime.UtcNow
            );
            
            brokerOrder.AddExecution(execution);
            
            // Update position
            var key = (order.CustomerId, order.Symbol);
            var currentPos = _positions.GetValueOrDefault(key, Quantity.Zero);
            _positions[key] = order.Side == OrderSide.Buy 
                ? currentPos + order.Quantity 
                : currentPos - order.Quantity;
        }

        return BrokerSubmitResult.Succeeded(orderId);
    }

    public Task<BrokerCancelResult> CancelOrderAsync(Guid orderIntentId, CancellationToken ct)
    {
        var brokerOrder = _orders.Values.FirstOrDefault(o => o.OrderIntentId == orderIntentId);
        
        if (brokerOrder == null)
            return Task.FromResult(new BrokerCancelResult(false, "Order not found"));

        if (brokerOrder.Status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected)
            return Task.FromResult(new BrokerCancelResult(false, $"Cannot cancel order in {brokerOrder.Status} state"));

        brokerOrder.Cancel();
        return Task.FromResult(new BrokerCancelResult(true, "Order cancelled successfully"));
    }

    public Task<BrokerOrderStatus> QueryOrderStatusAsync(Guid orderIntentId, CancellationToken ct)
    {
        var brokerOrder = _orders.Values.FirstOrDefault(o => o.OrderIntentId == orderIntentId);
        
        if (brokerOrder == null)
        {
            return Task.FromResult(new BrokerOrderStatus(
                OrderStatus.Unknown,
                Quantity.Zero,
                Quantity.Zero,
                Array.Empty<Execution>()));
        }

        return Task.FromResult(new BrokerOrderStatus(
            brokerOrder.Status,
            brokerOrder.FilledQuantity,
            brokerOrder.RemainingQuantity,
            brokerOrder.Executions));
    }

    public Task<Money> GetCashBalanceAsync(Guid customerId, CancellationToken ct)
    {
        var balance = _cashBalances.GetValueOrDefault(customerId, Money.Zero("PKR"));
        return Task.FromResult(balance);
    }

    public Task<Quantity> GetPositionAsync(Guid customerId, string symbol, CancellationToken ct)
    {
        var key = (customerId, symbol.ToUpperInvariant());
        var position = _positions.GetValueOrDefault(key, Quantity.Zero);
        return Task.FromResult(position);
    }

    public Task<bool> IsMarketOpenAsync(CancellationToken ct)
    {
        return Task.FromResult(_config.MarketOpen);
    }

    public Task<BrokerHealth> GetHealthAsync(CancellationToken ct)
    {
        if (!_config.IsHealthy)
            return Task.FromResult(BrokerHealth.Unavailable);
        
        return Task.FromResult(_config.IsDegraded ? BrokerHealth.Degraded : BrokerHealth.Healthy);
    }
}

/// <summary>
/// Configuration for paper broker behavior.
/// </summary>
public sealed class PaperBrokerConfig
{
    public string? ScenarioId { get; set; }
    public bool MarketOpen { get; set; } = true;
    public bool IsHealthy { get; set; } = true;
    public bool IsDegraded { get; set; } = false;
    public bool AutoFillMarketOrders { get; set; } = true;
    public bool AllowExecutionWhenMarketClosed { get; set; } = false;
    public bool AllowExecutionWhenUnavailable { get; set; } = false;
}
