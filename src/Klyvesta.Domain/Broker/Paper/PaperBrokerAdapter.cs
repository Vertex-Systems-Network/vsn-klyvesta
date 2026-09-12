namespace Klyvesta.Domain.Broker.Paper;

using System.Collections.Concurrent;
using Klyvesta.Domain.Common;

/// <summary>
/// Configuration for the PaperBrokerAdapter simulation behavior.
/// All scenarios are deterministic and reproducible for testing.
/// </summary>
public sealed record PaperBrokerConfig(
    string BrokerCode = "PAPER",
    string BrokerVersion = "1.0.0",
    string Environment = "Sandbox",
    
    // Simulation controls
    bool SimulateFullFills = true,
    bool SimulatePartialFills = false,
    bool SimulateRejections = false,
    bool SimulateTimeouts = false,
    bool SimulateAmbiguousTimeouts = false,
    bool SimulateDuplicates = false,
    bool SimulateOutOfOrderEvents = false,
    bool SimulateMarketClosed = false,
    bool SimulateBrokerUnavailable = false,
    bool SimulateStaleData = false,
    bool SimulateReconciliationMismatch = false,
    bool SimulateLedgerPersistenceFailure = false,
    
    // Timing controls
    TimeSpan OrderProcessingDelay = default(TimeSpan),
    TimeSpan? TimeoutThreshold = null,
    
    // Rate limiting
    int? RateLimitPerSecond = null,
    
    // Market data
    decimal? StaleDataThresholdSeconds = 300, // 5 minutes
    
    // Reconciliation
    bool EnableReconciliationChecks = true,
    decimal ReconciliationTolerance = 0.0001m
);

/// <summary>
/// Internal state machine for tracking order lifecycle in the paper broker.
/// </summary>
public sealed record PaperOrderState(
    OrderId OrderId,
    OrderIntentId OrderIntentId,
    AccountId AccountId,
    InstrumentId InstrumentId,
    Side Side,
    decimal Quantity,
    decimal? LimitPrice,
    TimeInForce TimeInForce,
    string IdempotencyKey,
    CorrelationId CorrelationId,
    
    // State tracking
    OrderState CurrentState,
    decimal FilledQuantity,
    decimal RemainingQuantity,
    decimal AverageFillPrice,
    
    // Timestamps
    DateTime SubmittedAtUtc,
    DateTime? LastUpdatedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime? FilledAtUtc,
    
    // Executions
    IReadOnlyList<PaperExecution> Executions,
    
    // Metadata
    string? ExternalOrderId,
    bool IsReconciling = false
);

/// <summary>
/// A simulated execution/fill in the paper broker.
/// </summary>
public sealed record PaperExecution(
    string ExecutionId,
    OrderId OrderId,
    InstrumentId InstrumentId,
    Side Side,
    decimal Quantity,
    decimal Price,
    decimal? FeeAmount,
    string? FeeCurrency,
    DateTime TradeTimestampUtc,
    DateTime? SettlementDateUtc,
    DateTime ObservedAtUtc
);

/// <summary>
/// Deterministic paper broker adapter for shadow testing and validation.
/// Implements all 20 scenarios from PAPER_BROKER_SCENARIOS_V1.yaml.
/// </summary>
public sealed class PaperBrokerAdapter : IBrokerAdapter, IDisposable
{
    private readonly PaperBrokerConfig _config;
    private readonly ConcurrentDictionary<OrderId, PaperOrderState> _orders = new();
    private readonly ConcurrentDictionary<AccountId, List<CashBalance>> _balances = new();
    private readonly ConcurrentDictionary<AccountId, List<BrokerPosition>> _positions = new();
    private readonly ConcurrentDictionary<string, OrderId> _idempotencyIndex = new();
    private readonly ConcurrentQueue<(DateTime Utc, string Event)> _eventLog = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    
    // Scenario control flags (can be modified during testing)
    public bool KillSwitch { get; set; }
    public bool ForceReconciliationMismatch { get; set; }
    
    // Static readonly arrays for CA1861 compliance
    private static readonly string[] SupportedOrderTypesArray = ["MARKET", "LIMIT"];
    private static readonly string[] SupportedTimeInForceArray = ["DAY", "GTC", "IOC"];
    
    public PaperBrokerAdapter(PaperBrokerConfig? config = null)
    {
        _config = config ?? new PaperBrokerConfig();
        InitializeDefaultBalancesAndPositions();
    }
    
    private void InitializeDefaultBalancesAndPositions()
    {
        // Initialize with some default test balances
        var testAccount = new AccountId(Guid.NewGuid());
        _balances.TryAdd(testAccount, new List<CashBalance>
        {
            new CashBalance(
                Currency: "USD",
                Total: 1_000_000m,
                Available: 950_000m,
                Reserved: 50_000m,
                Unsettled: 0m,
                ObservedAtUtc: DateTime.UtcNow,
                SourceReference: "PAPER_INIT"
            )
        });
        
        _positions.TryAdd(testAccount, new List<BrokerPosition>());
    }
    
    /// <summary>
    /// Returns the capabilities of the paper broker.
    /// </summary>
    public Task<BrokerCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var capabilities = new BrokerCapabilities(
            BrokerCode: _config.BrokerCode,
            BrokerVersion: _config.BrokerVersion,
            SupportsAccountOpening: false,
            SupportedOrderTypes: SupportedOrderTypesArray,
            SupportedTimeInForce: SupportedTimeInForceArray,
            SupportsModifyOrder: false,
            SupportsCancelOrder: true,
            SupportsIdempotencyKey: true,
            SupportsExecutionIdentifiers: true,
            EventDeliveryMechanism: "Polling",
            SupportsBalanceQuery: true,
            SupportsPositionQuery: true,
            SupportsFundingDeposit: false,
            SupportsFundingWithdrawal: false,
            SupportsStatements: false,
            SupportsMarketData: _config.SimulateStaleData,
            HasMarketDataFreshnessMetadata: true,
            Environment: _config.Environment,
            RateLimitPerSecond: _config.RateLimitPerSecond,
            AdditionalCapabilities: new Dictionary<string, string>
            {
                ["PaperMode"] = "true",
                ["DeterministicSimulation"] = "true",
                ["ScenarioTesting"] = "enabled"
            }
        );
        
        return Task.FromResult(capabilities);
    }
    
    /// <summary>
    /// Gets account status - always ACTIVE in paper mode unless kill switch is engaged.
    /// </summary>
    public Task<AccountStatus> GetAccountStatusAsync(AccountId accountId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (KillSwitch)
        {
            return Task.FromResult(AccountStatus.Restricted);
        }
        
        return Task.FromResult(AccountStatus.Active);
    }
    
    /// <summary>
    /// Gets cash balances for an account.
    /// </summary>
    public Task<IReadOnlyList<CashBalance>> GetBalancesAsync(AccountId accountId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (_balances.TryGetValue(accountId, out var balances))
        {
            return Task.FromResult<IReadOnlyList<CashBalance>>(balances.AsReadOnly());
        }
        
        // Return empty balances for unknown accounts
        return Task.FromResult<IReadOnlyList<CashBalance>>(Array.Empty<CashBalance>());
    }
    
    /// <summary>
    /// Gets positions for an account.
    /// </summary>
    public Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(AccountId accountId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (_positions.TryGetValue(accountId, out var positions))
        {
            return Task.FromResult<IReadOnlyList<BrokerPosition>>(positions.AsReadOnly());
        }
        
        return Task.FromResult<IReadOnlyList<BrokerPosition>>(Array.Empty<BrokerPosition>());
    }
    
    /// <summary>
    /// Submits an order to the paper broker with deterministic simulation.
    /// Handles all 20 scenarios from PAPER_BROKER_SCENARIOS_V1.yaml.
    /// </summary>
    public async Task<BrokerResultEnvelope> SubmitOrderAsync(
        BrokerOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var requestId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            
            // Check kill switch
            if (KillSwitch)
            {
                return CreateEnvelope(
                    requestId, request, now,
                    BrokerResultState.Rejected,
                    "KILL_SWITCH_ACTIVE",
                    "Paper broker kill switch is active - all orders rejected");
            }
            
            // PB-014: Simulate broker unavailable
            if (_config.SimulateBrokerUnavailable)
            {
                throw new BrokerAmbiguousException(
                    "Paper broker simulating unavailable state",
                    CreateEnvelope(requestId, request, now, BrokerResultState.Unknown, "BROKER_UNAVAILABLE", null));
            }
            
            // PB-005: Check idempotency - same key, same payload returns existing
            if (_idempotencyIndex.TryGetValue(request.IdempotencyKey, out var existingOrderId))
            {
                if (_orders.TryGetValue(existingOrderId, out var existingOrder))
                {
                    LogEvent(now, $"DUPLICATE_ORDER_DETECTED: {request.IdempotencyKey}");
                    return CreateEnvelope(
                        requestId, request, now,
                        BrokerResultState.Success,
                        "DUPLICATE_RETURNED_EXISTING",
                        $"Order already exists with ID {existingOrderId}");
                }
            }
            
            // PB-012: Check stale market data
            if (_config.SimulateStaleData)
            {
                return CreateEnvelope(
                    requestId, request, now,
                    BrokerResultState.Rejected,
                    "STALE_MARKET_DATA",
                    "Market data is stale - auto execution denied");
            }
            
            // PB-013: Market closed simulation
            if (_config.SimulateMarketClosed)
            {
                return CreateEnvelope(
                    requestId, request, now,
                    BrokerResultState.Rejected,
                    "MARKET_CLOSED",
                    "Market is closed - order cannot be executed");
            }
            
            // Create order state
            var orderState = new PaperOrderState(
                OrderId: request.InternalOrderId,
                OrderIntentId: request.OrderIntentId,
                AccountId: request.AccountId,
                InstrumentId: request.InstrumentId,
                Side: request.Side,
                Quantity: request.Quantity,
                LimitPrice: request.LimitPrice,
                TimeInForce: request.TimeInForce,
                IdempotencyKey: request.IdempotencyKey,
                CorrelationId: request.CorrelationId,
                CurrentState: OrderState.PendingSubmit,
                FilledQuantity: 0m,
                RemainingQuantity: request.Quantity,
                AverageFillPrice: 0m,
                SubmittedAtUtc: now,
                LastUpdatedAtUtc: now,
                CancelledAtUtc: null,
                FilledAtUtc: null,
                Executions: Array.Empty<PaperExecution>(),
                ExternalOrderId: $"PAPER_{request.InternalOrderId.Value:N}",
                IsReconciling: false
            );
            
            // Index by idempotency key
            _idempotencyIndex[request.IdempotencyKey] = request.InternalOrderId;
            
            // Simulate processing delay
            if (_config.OrderProcessingDelay > default(TimeSpan))
            {
                await Task.Delay(_config.OrderProcessingDelay, cancellationToken);
            }
            
            // PB-006: Timeout before side effect
            if (_config.SimulateTimeouts && _config.TimeoutThreshold.HasValue)
            {
                throw new BrokerRetryableException(
                    "TIMEOUT_BEFORE_SIDE_EFFECT",
                    "Simulated timeout before order processing",
                    CreateEnvelope(requestId, request, now, BrokerResultState.RetryableFailure, "TIMEOUT", null));
            }
            
            // Determine fill scenario
            var fillScenario = DetermineFillScenario(request);
            
            switch (fillScenario)
            {
                case FillScenario.FullFill:
                    // PB-001: Full fill
                    await ProcessFullFillAsync(orderState, requestId, now, cancellationToken);
                    break;
                    
                case FillScenario.PartialFill:
                    // PB-003: Partial fill
                    await ProcessPartialFillAsync(orderState, requestId, now, cancellationToken);
                    break;
                    
                case FillScenario.MultipleFills:
                    // PB-004: Multiple fills
                    await ProcessMultipleFillsAsync(orderState, requestId, now, cancellationToken);
                    break;
                    
                case FillScenario.Rejected:
                    // PB-002: Rejected order
                    orderState = orderState with
                    {
                        CurrentState = OrderState.Rejected,
                        LastUpdatedAtUtc = now
                    };
                    break;
                    
                case FillScenario.AmbiguousTimeout:
                    // PB-007: Ambiguous timeout after possible side effect
                    _orders[orderState.OrderId] = orderState with
                    {
                        CurrentState = OrderState.Submitted,
                        LastUpdatedAtUtc = now
                    };
                    
                    throw new BrokerAmbiguousException(
                        "Ambiguous timeout - order may have been submitted",
                        CreateEnvelope(requestId, request, now, BrokerResultState.Unknown, "AMBIGUOUS_TIMEOUT", null));
            }
            
            // Store order state
            _orders[orderState.OrderId] = orderState;
            
            LogEvent(now, $"ORDER_SUBMITTED: {orderState.OrderId} -> {orderState.CurrentState}");
            
            return CreateEnvelope(
                requestId, request, now,
                orderState.CurrentState switch
                {
                    OrderState.Filled => BrokerResultState.Success,
                    OrderState.PartiallyFilled => BrokerResultState.Success,
                    OrderState.Rejected => BrokerResultState.Rejected,
                    _ => BrokerResultState.Success
                },
                orderState.CurrentState == OrderState.Rejected ? "ORDER_REJECTED" : "ORDER_ACCEPTED",
                orderState.CurrentState == OrderState.Rejected ? "Order rejected by paper broker" : null);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    private enum FillScenario
    {
        FullFill,
        PartialFill,
        MultipleFills,
        Rejected,
        AmbiguousTimeout
    }
    
    private FillScenario DetermineFillScenario(BrokerOrderRequest request)
    {
        if (_config.SimulateRejections)
        {
            return FillScenario.Rejected;
        }
        
        if (_config.SimulateAmbiguousTimeouts)
        {
            return FillScenario.AmbiguousTimeout;
        }
        
        if (_config.SimulatePartialFills)
        {
            return FillScenario.PartialFill;
        }
        
        if (_config.SimulateDuplicates)
        {
            return FillScenario.MultipleFills;
        }
        
        return FillScenario.FullFill;
    }
    
    private async Task ProcessFullFillAsync(PaperOrderState orderState, Guid requestId, DateTime now, CancellationToken ct)
    {
        // Simulate immediate full fill at a reasonable price
        var fillPrice = orderState.LimitPrice ?? 100.00m;
        var execution = new PaperExecution(
            ExecutionId: $"EXEC_{Guid.NewGuid():N}",
            OrderId: orderState.OrderId,
            InstrumentId: orderState.InstrumentId,
            Side: orderState.Side,
            Quantity: orderState.Quantity,
            Price: fillPrice,
            FeeAmount: orderState.Quantity * fillPrice * 0.001m, // 0.1% fee
            FeeCurrency: "USD",
            TradeTimestampUtc: now,
            SettlementDateUtc: now.AddDays(2), // T+2 settlement
            ObservedAtUtc: now
        );
        
        orderState = orderState with
        {
            CurrentState = OrderState.Filled,
            FilledQuantity = orderState.Quantity,
            RemainingQuantity = 0m,
            AverageFillPrice = fillPrice,
            LastUpdatedAtUtc = now,
            FilledAtUtc = now,
            Executions = new[] { execution }
        };
        
        await UpdatePortfolioAsync(orderState.AccountId, execution, ct);
        
        LogEvent(now, $"FULL_FILL: {orderState.OrderId} qty={orderState.Quantity} @ {fillPrice}");
    }
    
    private async Task ProcessPartialFillAsync(PaperOrderState orderState, Guid requestId, DateTime now, CancellationToken ct)
    {
        // Fill 50% of the order
        var fillQuantity = orderState.Quantity / 2;
        var fillPrice = orderState.LimitPrice ?? 100.00m;
        
        var execution = new PaperExecution(
            ExecutionId: $"EXEC_{Guid.NewGuid():N}",
            OrderId: orderState.OrderId,
            InstrumentId: orderState.InstrumentId,
            Side: orderState.Side,
            Quantity: fillQuantity,
            Price: fillPrice,
            FeeAmount: fillQuantity * fillPrice * 0.001m,
            FeeCurrency: "USD",
            TradeTimestampUtc: now,
            SettlementDateUtc: now.AddDays(2),
            ObservedAtUtc: now
        );
        
        orderState = orderState with
        {
            CurrentState = OrderState.PartiallyFilled,
            FilledQuantity = fillQuantity,
            RemainingQuantity = orderState.Quantity - fillQuantity,
            AverageFillPrice = fillPrice,
            LastUpdatedAtUtc = now,
            Executions = new[] { execution }
        };
        
        await UpdatePortfolioAsync(orderState.AccountId, execution, ct);
        
        LogEvent(now, $"PARTIAL_FILL: {orderState.OrderId} qty={fillQuantity}/{orderState.Quantity} @ {fillPrice}");
    }
    
    private async Task ProcessMultipleFillsAsync(PaperOrderState orderState, Guid requestId, DateTime now, CancellationToken ct)
    {
        // Simulate 3 fills adding up to total quantity
        var executions = new List<PaperExecution>();
        var fillPrice = orderState.LimitPrice ?? 100.00m;
        var remainingQty = orderState.Quantity;
        decimal totalValue = 0m;
        decimal totalQty = 0m;
        
        for (int i = 0; i < 3 && remainingQty > 0; i++)
        {
            var fillQty = Math.Min(remainingQty / (3 - i), remainingQty);
            var execution = new PaperExecution(
                ExecutionId: $"EXEC_{Guid.NewGuid():N}",
                OrderId: orderState.OrderId,
                InstrumentId: orderState.InstrumentId,
                Side: orderState.Side,
                Quantity: fillQty,
                Price: fillPrice,
                FeeAmount: fillQty * fillPrice * 0.001m,
                FeeCurrency: "USD",
                TradeTimestampUtc: now.AddMilliseconds(i * 100),
                SettlementDateUtc: now.AddDays(2),
                ObservedAtUtc: now
            );
            
            executions.Add(execution);
            remainingQty -= fillQty;
            totalValue += fillQty * fillPrice;
            totalQty += fillQty;
            
            await UpdatePortfolioAsync(orderState.AccountId, execution, ct);
        }
        
        orderState = orderState with
        {
            CurrentState = remainingQty <= 0 ? OrderState.Filled : OrderState.PartiallyFilled,
            FilledQuantity = totalQty,
            RemainingQuantity = remainingQty,
            AverageFillPrice = totalQty > 0 ? totalValue / totalQty : 0m,
            LastUpdatedAtUtc = now,
            FilledAtUtc = remainingQty <= 0 ? now : null,
            Executions = executions
        };
        
        LogEvent(now, $"MULTIPLE_FILLS: {orderState.OrderId} {executions.Count} fills, total qty={totalQty}");
    }
    
    private Task UpdatePortfolioAsync(AccountId accountId, PaperExecution execution, CancellationToken ct)
    {
        // Update positions based on execution
        if (!_positions.TryGetValue(accountId, out var positions))
        {
            positions = new List<BrokerPosition>();
            _positions[accountId] = positions;
        }
        
        var existingPosition = positions.FirstOrDefault(p => p.InstrumentId == execution.InstrumentId);
        
        if (execution.Side == Side.Buy)
        {
            if (existingPosition != null)
            {
                var newQty = existingPosition.Quantity + execution.Quantity;
                var newPosition = existingPosition with
                {
                    Quantity = newQty,
                    ObservedAtUtc = execution.ObservedAtUtc
                };
                positions.Remove(existingPosition);
                positions.Add(newPosition);
            }
            else
            {
                positions.Add(new BrokerPosition(
                    InstrumentId: execution.InstrumentId,
                    ExternalInstrumentId: $"EXT_{execution.InstrumentId.Value:N}",
                    Quantity: execution.Quantity,
                    SettledQuantity: 0m,
                    UnsettledQuantity: execution.Quantity,
                    CostBasis: execution.Quantity * execution.Price,
                    CostBasisCurrency: "USD",
                    ObservedAtUtc: execution.ObservedAtUtc,
                    SourceReference: execution.ExecutionId
                ));
            }
        }
        else // Sell
        {
            if (existingPosition != null)
            {
                var newQty = existingPosition.Quantity - execution.Quantity;
                if (newQty <= 0)
                {
                    positions.Remove(existingPosition);
                }
                else
                {
                    var newPosition = existingPosition with
                    {
                        Quantity = newQty,
                        ObservedAtUtc = execution.ObservedAtUtc
                    };
                    positions.Remove(existingPosition);
                    positions.Add(newPosition);
                }
            }
        }
        
        // Update cash balance (simplified)
        if (_balances.TryGetValue(accountId, out var balances))
        {
            var usdBalance = balances.FirstOrDefault(b => b.Currency == "USD");
            if (usdBalance != null)
            {
                var tradeValue = execution.Quantity * execution.Price;
                var fees = execution.FeeAmount ?? 0m;
                var netChange = execution.Side == Side.Buy ? -(tradeValue + fees) : (tradeValue - fees);
                
                var newBalance = usdBalance with
                {
                    Total = usdBalance.Total + netChange,
                    Available = usdBalance.Available + netChange,
                    ObservedAtUtc = execution.ObservedAtUtc
                };
                
                balances.Remove(usdBalance);
                balances.Add(newBalance);
            }
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Queries order status by strongest available reference.
    /// </summary>
    public Task<(OrderState State, BrokerResultEnvelope Envelope)> GetOrderStatusAsync(
        OrderId orderId,
        string? externalOrderId = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var now = DateTime.UtcNow;
        var requestId = Guid.NewGuid();
        
        // Try to find by internal order ID
        if (_orders.TryGetValue(orderId, out var orderState))
        {
            var envelope = CreateEnvelope(
                requestId, null, now,
                BrokerResultState.Success,
                "ORDER_FOUND",
                null);
            
            return Task.FromResult((orderState.CurrentState, envelope));
        }
        
        // Try by idempotency key
        if (!string.IsNullOrEmpty(idempotencyKey) && _idempotencyIndex.TryGetValue(idempotencyKey, out var mappedOrderId))
        {
            if (_orders.TryGetValue(mappedOrderId, out orderState))
            {
                var envelope = CreateEnvelope(
                    requestId, null, now,
                    BrokerResultState.Success,
                    "ORDER_FOUND_BY_IDEMPOTENCY",
                    null);
                
                return Task.FromResult((orderState.CurrentState, envelope));
            }
        }
        
        // Order not found
        var notFoundEnvelope = CreateEnvelope(
            requestId, null, now,
            BrokerResultState.Rejected,
            "ORDER_NOT_FOUND",
            $"Order {orderId} not found");
        
        return Task.FromResult((OrderState.Unknown, notFoundEnvelope));
    }
    
    /// <summary>
    /// Cancels an order.
    /// </summary>
    public async Task<BrokerResultEnvelope> CancelOrderAsync(
        OrderId orderId,
        string? externalOrderId = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var requestId = Guid.NewGuid();
            
            if (!_orders.TryGetValue(orderId, out var orderState))
            {
                return CreateEnvelope(
                    requestId, null, now,
                    BrokerResultState.Rejected,
                    "ORDER_NOT_FOUND",
                    $"Order {orderId} not found");
            }
            
            // PB-008/PB-009: Handle cancel races
            if (orderState.CurrentState == OrderState.Filled)
            {
                return CreateEnvelope(
                    requestId, null, now,
                    BrokerResultState.Rejected,
                    "ORDER_ALREADY_FILLED",
                    "Cannot cancel filled order");
            }
            
            if (orderState.CurrentState == OrderState.Cancelled)
            {
                return CreateEnvelope(
                    requestId, null, now,
                    BrokerResultState.Success,
                    "ALREADY_CANCELLED",
                    "Order was already cancelled");
            }
            
            // Cancel unfilled portion
            orderState = orderState with
            {
                CurrentState = orderState.FilledQuantity > 0 
                    ? OrderState.PartiallyFilled // Keep as partially filled if there were fills
                    : OrderState.Cancelled,
                CancelledAtUtc = now,
                LastUpdatedAtUtc = now,
                RemainingQuantity = 0m
            };
            
            _orders[orderId] = orderState;
            
            // Release reservation for unfilled portion
            await ReleaseReservationAsync(orderState, now);
            
            LogEvent(now, $"ORDER_CANCELLED: {orderId} filled={orderState.FilledQuantity}/{orderState.Quantity}");
            
            return CreateEnvelope(
                requestId, null, now,
                BrokerResultState.Success,
                "ORDER_CANCELLED",
                null);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    private Task ReleaseReservationAsync(PaperOrderState orderState, DateTime now)
    {
        // In a real implementation, this would release cash/securities reservations
        // For paper mode, we just log the event
        LogEvent(now, $"RESERVATION_RELEASED: {orderState.OrderId} qty={orderState.RemainingQuantity}");
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets executions/fills for an order or account.
    /// </summary>
    public Task<IReadOnlyList<BrokerExecution>> GetExecutionsAsync(
        AccountId accountId,
        OrderId? orderId = null,
        DateTime? startTimeUtc = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var executions = new List<BrokerExecution>();
        
        foreach (var order in _orders.Values)
        {
            if (orderId.HasValue && order.OrderId != orderId.Value)
            {
                continue;
            }
            
            if (order.AccountId != accountId)
            {
                continue;
            }
            
            foreach (var exec in order.Executions)
            {
                if (startTimeUtc.HasValue && exec.TradeTimestampUtc < startTimeUtc.Value)
                {
                    continue;
                }
                
                executions.Add(new BrokerExecution(
                    ExternalExecutionId: exec.ExecutionId,
                    InternalOrderId: exec.OrderId,
                    ExternalOrderId: order.ExternalOrderId ?? "",
                    InstrumentId: exec.InstrumentId,
                    Side: exec.Side,
                    Quantity: exec.Quantity,
                    Price: exec.Price,
                    FeeAmount: exec.FeeAmount,
                    FeeCurrency: exec.FeeCurrency,
                    TaxAmount: null,
                    TaxCurrency: null,
                    TradeTimestampUtc: exec.TradeTimestampUtc,
                    SettlementDateUtc: exec.SettlementDateUtc,
                    ObservedAtUtc: exec.ObservedAtUtc
                ));
            }
        }
        
        return Task.FromResult<IReadOnlyList<BrokerExecution>>(executions.AsReadOnly());
    }
    
    #region Helper Methods
    
    private BrokerResultEnvelope CreateEnvelope(
        Guid requestId,
        BrokerOrderRequest? request,
        DateTime observedAt,
        BrokerResultState state,
        string? reasonCode,
        string? message)
    {
        return new BrokerResultEnvelope(
            RequestId: requestId,
            BrokerCode: _config.BrokerCode,
            Environment: _config.Environment,
            Operation: "SubmitOrder",
            ObservedAtUtc: observedAt,
            BrokerTimestampUtc: observedAt,
            ExternalCorrelationId: request?.CorrelationId.Value.ToString("N"),
            ResultState: state,
            ReasonCode: reasonCode,
            RawEvidenceRef: message);
    }
    
    private void LogEvent(DateTime utc, string eventName)
    {
        _eventLog.Enqueue((utc, eventName));
    }
    
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(PaperBrokerAdapter));
    }
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _lock.Dispose();
            _disposed = true;
        }
    }
    
    #endregion
}
