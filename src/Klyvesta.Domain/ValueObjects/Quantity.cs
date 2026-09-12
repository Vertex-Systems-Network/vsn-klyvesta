namespace Klyvesta.Domain.ValueObjects;

/// <summary>
/// Decimal-based quantity for securities. Never use floating-point for quantities.
/// </summary>
public sealed class Quantity : IEquatable<Quantity>, IComparable<Quantity>
{
    public decimal Amount { get; }

    public static readonly Quantity Zero = new(0m);

    public Quantity(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentException("Quantity cannot be negative", nameof(amount));
        
        Amount = amount;
    }

    public static Quantity operator +(Quantity left, Quantity right)
        => new(left.Amount + right.Amount);

    public static Quantity operator -(Quantity left, Quantity right)
    {
        var result = left.Amount - right.Amount;
        if (result < 0)
            throw new InvalidOperationException("Subtraction resulted in negative quantity");
        return new Quantity(result);
    }

    public static Quantity operator *(Quantity quantity, decimal multiplier)
    {
        if (multiplier < 0)
            throw new ArgumentException("Multiplier cannot be negative", nameof(multiplier));
        return new Quantity(quantity.Amount * multiplier);
    }

    public static bool operator ==(Quantity? left, Quantity? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(Quantity? left, Quantity? right) => !(left == right);

    public static bool operator <(Quantity left, Quantity right)
        => left.Amount < right.Amount;

    public static bool operator >(Quantity left, Quantity right)
        => left.Amount > right.Amount;

    public bool Equals(Quantity? other)
    {
        if (other is null) return false;
        return Amount == other.Amount;
    }

    public override bool Equals(object? obj) => obj is Quantity other && Equals(other);

    public override int GetHashCode() => Amount.GetHashCode();

    public int CompareTo(Quantity? other)
    {
        if (other is null) return 1;
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() => Amount.ToString("N4");
}
