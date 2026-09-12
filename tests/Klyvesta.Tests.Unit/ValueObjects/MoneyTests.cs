using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Tests.Unit.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithPositiveAmount_CreatesMoneySuccessfully()
    {
        var money = new Money(100.50m);
        
        Assert.Equal(100.50m, money.Amount);
        Assert.Equal("PKR", money.Currency);
    }
    
    [Fact]
    public void Constructor_WithZeroAmount_CreatesMoneySuccessfully()
    {
        var money = new Money(0m);
        
        Assert.Equal(0m, money.Amount);
        Assert.Equal("PKR", money.Currency);
    }
    
    [Fact]
    public void Constructor_WithNegativeAmount_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Money(-1m));
    }
    
    [Fact]
    public void Constructor_WithCustomCurrency_UsesProvidedCurrency()
    {
        var money = new Money(100m, "USD");
        
        Assert.Equal("USD", money.Currency);
    }
    
    [Fact]
    public void Addition_SameCurrency_ReturnsSum()
    {
        var left = new Money(100m);
        var right = new Money(50m);
        
        var result = left + right;
        
        Assert.Equal(150m, result.Amount);
        Assert.Equal("PKR", result.Currency);
    }
    
    [Fact]
    public void Addition_DifferentCurrencies_ThrowsInvalidOperationException()
    {
        var left = new Money(100m, "PKR");
        var right = new Money(50m, "USD");
        
        Assert.Throws<InvalidOperationException>(() => left + right);
    }
    
    [Fact]
    public void Subtraction_SameCurrency_ReturnsDifference()
    {
        var left = new Money(100m);
        var right = new Money(30m);
        
        var result = left - right;
        
        Assert.Equal(70m, result.Amount);
        Assert.Equal("PKR", result.Currency);
    }
    
    [Fact]
    public void Subtraction_ResultingInNegative_ThrowsInvalidOperationException()
    {
        var left = new Money(50m);
        var right = new Money(100m);
        
        Assert.Throws<InvalidOperationException>(() => left - right);
    }
    
    [Fact]
    public void Multiplication_ByPositiveDecimal_ReturnsProduct()
    {
        var money = new Money(100m);
        
        var result = money * 2.5m;
        
        Assert.Equal(250m, result.Amount);
        Assert.Equal("PKR", result.Currency);
    }
    
    [Fact]
    public void Multiplication_ByNegativeDecimal_ThrowsArgumentException()
    {
        var money = new Money(100m);
        
        Assert.Throws<ArgumentException>(() => money * -1m);
    }
    
    [Fact]
    public void Equality_SameAmountAndCurrency_ReturnsTrue()
    {
        var money1 = new Money(100m, "PKR");
        var money2 = new Money(100m, "PKR");
        
        Assert.True(money1 == money2);
        Assert.True(money1.Equals(money2));
    }
    
    [Fact]
    public void Equality_DifferentAmount_ReturnsFalse()
    {
        var money1 = new Money(100m, "PKR");
        var money2 = new Money(50m, "PKR");
        
        Assert.False(money1 == money2);
        Assert.False(money1.Equals(money2));
    }
    
    [Fact]
    public void Equality_DifferentCurrency_ReturnsFalse()
    {
        var money1 = new Money(100m, "PKR");
        var money2 = new Money(100m, "USD");
        
        Assert.False(money1 == money2);
        Assert.False(money1.Equals(money2));
    }
    
    [Fact]
    public void Comparison_LessThan_ReturnsTrue()
    {
        var left = new Money(50m);
        var right = new Money(100m);
        
        Assert.True(left < right);
        Assert.False(left > right);
    }
    
    [Fact]
    public void Comparison_GreaterThan_ReturnsTrue()
    {
        var left = new Money(150m);
        var right = new Money(100m);
        
        Assert.True(left > right);
        Assert.False(left < right);
    }
    
    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var money = new Money(1234.56m);
        
        var result = money.ToString();
        
        Assert.Equal("1,234.56 PKR", result);
    }
    
    [Fact]
    public void Zero_ReturnsZeroMoney()
    {
        Assert.Equal(0m, Money.Zero.Amount);
        Assert.Equal("PKR", Money.Zero.Currency);
    }
}
