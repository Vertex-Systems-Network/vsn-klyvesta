namespace Klyvesta.Domain.Common;

/// <summary>
/// Money value using decimal for exact financial calculations.
/// Never use float/double for monetary amounts.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money Zero(string currency = "PKR") => new(0m, currency);
    
    public Money Abs() => new(Math.Abs(Amount), Currency);
    
    public bool IsPositive => Amount > 0;
    public bool IsNegative => Amount < 0;
    public bool IsZero => Amount == 0;
    
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot add money with different currencies: {left.Currency} and {right.Currency}");
        return new Money(left.Amount + right.Amount, left.Currency);
    }
    
    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot subtract money with different currencies: {left.Currency} and {right.Currency}");
        return new Money(left.Amount - right.Amount, left.Currency);
    }
    
    public static Money operator *(Money money, decimal factor)
        => new(money.Amount * factor, money.Currency);
    
    public static Money operator *(decimal factor, Money money)
        => new(money.Amount * factor, money.Currency);
    
    public static bool operator >(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot compare money with different currencies: {left.Currency} and {right.Currency}");
        return left.Amount > right.Amount;
    }
    
    public static bool operator <(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot compare money with different currencies: {left.Currency} and {right.Currency}");
        return left.Amount < right.Amount;
    }
}

/// <summary>
/// Quantity of shares/units. Uses decimal to support fractional shares where permitted.
/// </summary>
public readonly record struct Quantity(decimal Value)
{
    public static Quantity Zero => new(0m);
    public static Quantity One => new(1m);
    
    public bool IsPositive => Value > 0;
    public bool IsNegative => Value < 0;
    public bool IsZero => Value == 0;
    
    public static Quantity operator +(Quantity left, Quantity right)
        => new(left.Value + right.Value);
    
    public static Quantity operator -(Quantity left, Quantity right)
        => new(left.Value - right.Value);
    
    public static Quantity operator *(Quantity quantity, decimal factor)
        => new(quantity.Value * factor);
    
    public static bool operator >(Quantity left, Quantity right)
        => left.Value > right.Value;
    
    public static bool operator <(Quantity left, Quantity right)
        => left.Value < right.Value;
}

/// <summary>
/// Price per share/unit with exact decimal representation.
/// </summary>
public readonly record struct Price(decimal Value, string Currency)
{
    public static Price Zero(string currency = "PKR") => new(0m, currency);
    
    public bool IsPositive => Value > 0;
    public bool IsZero => Value == 0;
    
    public Money ToMoney(Quantity quantity)
    {
        if (Currency != quantity.IsZero ? "PKR" : Currency) // Simplified - real impl needs currency awareness
            throw new InvalidOperationException("Currency mismatch in price-to-money conversion");
        return new Money(Value * quantity.Value, Currency);
    }
    
    public static Price operator +(Price left, Price right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot add prices with different currencies");
        return new Price(left.Value + right.Value, left.Currency);
    }
}

/// <summary>
/// Percentage value for concentrations, allocations, risk metrics.
/// </summary>
public readonly record struct Percentage(decimal Value)
{
    public static Percentage Zero => new(0m);
    public static Percentage Hundred => new(100m);
    
    /// <summary>
    /// Value as a decimal fraction (e.g., 5% -> 0.05)
    /// </summary>
    public decimal AsFraction => Value / 100m;
    
    public bool IsValid => Value >= 0 && Value <= 100;
    
    public static Percentage FromFraction(decimal fraction)
        => new(fraction * 100m);
}
