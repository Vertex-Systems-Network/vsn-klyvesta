namespace Klyvesta.Domain.ValueObjects;

/// <summary>
/// Instrument identifier (e.g., stock symbol).
/// </summary>
public sealed class Symbol : IEquatable<Symbol>
{
    public string Value { get; }

    public Symbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Symbol value is required", nameof(value));
        
        Value = value.ToUpperInvariant();
    }

    public bool Equals(Symbol? other)
    {
        if (other is null) return false;
        return Value == other.Value;
    }

    public override bool Equals(object? obj) => obj is Symbol other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(Symbol? left, Symbol? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(Symbol? left, Symbol? right) => !(left == right);

    public override string ToString() => Value;
}
