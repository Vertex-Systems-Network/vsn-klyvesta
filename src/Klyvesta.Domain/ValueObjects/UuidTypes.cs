using System;

namespace Klyvesta.Domain.ValueObjects;

/// <summary>
/// UUIDv7-based Account ID.
/// </summary>
public sealed class AccountId : IEquatable<AccountId>
{
    public Guid Value { get; }

    public AccountId(Guid value)
    {
        Value = value;
    }

    public static AccountId New() => new AccountId(Guid.CreateVersion7());

    public bool Equals(AccountId? other)
    {
        if (other is null) return false;
        return Value == other.Value;
    }

    public override bool Equals(object? obj) => obj is AccountId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(AccountId? left, AccountId? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(AccountId? left, AccountId? right) => !(left == right);

    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// UUIDv7-based Order ID.
/// </summary>
public sealed class OrderId : IEquatable<OrderId>
{
    public Guid Value { get; }

    public OrderId(Guid value)
    {
        Value = value;
    }

    public static OrderId New() => new OrderId(Guid.CreateVersion7());

    public bool Equals(OrderId? other)
    {
        if (other is null) return false;
        return Value == other.Value;
    }

    public override bool Equals(object? obj) => obj is OrderId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(OrderId? left, OrderId? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(OrderId? left, OrderId? right) => !(left == right);

    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// UUIDv7-based Execution ID.
/// </summary>
public sealed class ExecutionId : IEquatable<ExecutionId>
{
    public Guid Value { get; }

    public ExecutionId(Guid value)
    {
        Value = value;
    }

    public static ExecutionId New() => new ExecutionId(Guid.CreateVersion7());

    public bool Equals(ExecutionId? other)
    {
        if (other is null) return false;
        return Value == other.Value;
    }

    public override bool Equals(object? obj) => obj is ExecutionId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(ExecutionId? left, ExecutionId? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(ExecutionId? left, ExecutionId? right) => !(left == right);

    public override string ToString() => Value.ToString("D");
}
