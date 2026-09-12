namespace Klyvesta.Domain.Common;

/// <summary>
/// Strongly-typed identifier for customers.
/// </summary>
public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for accounts/portfolios.
/// </summary>
public readonly record struct AccountId(Guid Value)
{
    public static AccountId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for instruments (securities, ETFs, etc.).
/// </summary>
public readonly record struct InstrumentId(Guid Value)
{
    public static InstrumentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for orders.
/// </summary>
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for order intents (pre-execution orders).
/// </summary>
public readonly record struct OrderIntentId(Guid Value)
{
    public static OrderIntentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for executions/fills.
/// </summary>
public readonly record struct ExecutionId(Guid Value)
{
    public static ExecutionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for ledger accounts.
/// </summary>
public readonly record struct LedgerAccountId(Guid Value)
{
    public static LedgerAccountId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for ledger journals.
/// </summary>
public readonly record struct JournalId(Guid Value)
{
    public static JournalId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for positions.
/// </summary>
public readonly record struct PositionId(Guid Value)
{
    public static PositionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for mandates (Guarded Auto authorization).
/// </summary>
public readonly record struct MandateId(Guid Value)
{
    public static MandateId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for AI proposals/recommendations.
/// </summary>
public readonly record struct AiProposalId(Guid Value)
{
    public static AiProposalId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for risk decisions.
/// </summary>
public readonly record struct RiskDecisionId(Guid Value)
{
    public static RiskDecisionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for compliance decisions.
/// </summary>
public readonly record struct ComplianceDecisionId(Guid Value)
{
    public static ComplianceDecisionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Correlation ID for tracing requests across services.
/// </summary>
public readonly record struct CorrelationId(Guid Value)
{
    public static CorrelationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Idempotency key to prevent duplicate command processing.
/// </summary>
public readonly record struct IdempotencyKey(string Value)
{
    public override string ToString() => Value;
    
    public static IdempotencyKey New() => new(Guid.NewGuid().ToString("N"));
}
