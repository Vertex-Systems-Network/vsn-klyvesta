namespace Klyvesta.Infrastructure.Persistence.Records;

/// <summary>
/// Order intent persistence record.
/// </summary>
internal sealed class OrderIntentRecord
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public int Side { get; set; } // Enum: OrderSide
    public int Type { get; set; } // Enum: OrderType
    public decimal Quantity { get; set; }
    public decimal? LimitPrice { get; set; }
    public string? Currency { get; set; }
    public int TimeInForce { get; set; } // Enum: TimeInForce
    public int Status { get; set; } // Enum: OrderStatus
    public string? IdempotencyKey { get; set; }
    public Guid? BrokerOrderId { get; set; }
    public int? RejectionReason { get; set; } // Enum: RejectionReason
    public string? RejectionDetails { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
}

/// <summary>
/// Execution/fill persistence record.
/// </summary>
internal sealed class ExecutionRecord
{
    public Guid Id { get; set; }
    public Guid OrderIntentId { get; set; }
    public Guid CustomerId { get; set; }
    public string ExecutionId { get; set; } = string.Empty; // Stable ID from broker
    public decimal Quantity { get; set; }
    public decimal PricePerUnit { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime ExecutedAtUtc { get; set; }
    public string? BrokerReference { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}

/// <summary>
/// Position persistence record.
/// </summary>
internal sealed class PositionRecord
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AverageCostBasis { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime LastUpdatedAtUtc { get; set; }
}

/// <summary>
/// Ledger transaction (journal) persistence record.
/// </summary>
internal sealed class LedgerJournalRecord
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int Status { get; set; } // 0=Pending, 1=Posted, 2=Failed
}

/// <summary>
/// Ledger posting line persistence record.
/// </summary>
internal sealed class LedgerPostingRecord
{
    public Guid Id { get; set; }
    public Guid JournalId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsDebit { get; set; }
    public string? Reference { get; set; }
    public DateTime PostedAtUtc { get; set; }
}

/// <summary>
/// Broker order persistence record.
/// </summary>
internal sealed class BrokerOrderRecord
{
    public Guid Id { get; set; }
    public Guid OrderIntentId { get; set; }
    public Guid CustomerId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public int Side { get; set; }
    public int Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal? LimitPrice { get; set; }
    public string? Currency { get; set; }
    public int TimeInForce { get; set; }
    public int Status { get; set; }
    public string? BrokerOrderId { get; set; } // Assigned by broker
    public int? RejectionReason { get; set; }
    public string? RejectionDetails { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
