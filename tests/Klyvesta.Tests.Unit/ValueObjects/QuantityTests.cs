using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Tests.Unit.ValueObjects;

public class QuantityTests
{
    [Fact]
    public void Constructor_WithPositiveAmount_CreatesQuantitySuccessfully()
    {
        var quantity = new Quantity(100.50m);
        
        Assert.Equal(100.50m, quantity.Amount);
    }
    
    [Fact]
    public void Constructor_WithZeroAmount_CreatesQuantitySuccessfully()
    {
        var quantity = new Quantity(0m);
        
        Assert.Equal(0m, quantity.Amount);
    }
    
    [Fact]
    public void Constructor_WithNegativeAmount_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Quantity(-1m));
    }
    
    [Fact]
    public void Addition_ReturnsSum()
    {
        var left = new Quantity(100m);
        var right = new Quantity(50m);
        
        var result = left + right;
        
        Assert.Equal(150m, result.Amount);
    }
    
    [Fact]
    public void Subtraction_ReturnsDifference()
    {
        var left = new Quantity(100m);
        var right = new Quantity(30m);
        
        var result = left - right;
        
        Assert.Equal(70m, result.Amount);
    }
    
    [Fact]
    public void Subtraction_ResultingInNegative_ThrowsInvalidOperationException()
    {
        var left = new Quantity(50m);
        var right = new Quantity(100m);
        
        Assert.Throws<InvalidOperationException>(() => left - right);
    }
    
    [Fact]
    public void Multiplication_ByPositiveDecimal_ReturnsProduct()
    {
        var quantity = new Quantity(100m);
        
        var result = quantity * 2.5m;
        
        Assert.Equal(250m, result.Amount);
    }
    
    [Fact]
    public void Multiplication_ByNegativeDecimal_ThrowsArgumentException()
    {
        var quantity = new Quantity(100m);
        
        Assert.Throws<ArgumentException>(() => quantity * -1m);
    }
    
    [Fact]
    public void Equality_SameAmount_ReturnsTrue()
    {
        var quantity1 = new Quantity(100m);
        var quantity2 = new Quantity(100m);
        
        Assert.True(quantity1 == quantity2);
        Assert.True(quantity1.Equals(quantity2));
    }
    
    [Fact]
    public void Equality_DifferentAmount_ReturnsFalse()
    {
        var quantity1 = new Quantity(100m);
        var quantity2 = new Quantity(50m);
        
        Assert.False(quantity1 == quantity2);
        Assert.False(quantity1.Equals(quantity2));
    }
    
    [Fact]
    public void Comparison_LessThan_ReturnsTrue()
    {
        var left = new Quantity(50m);
        var right = new Quantity(100m);
        
        Assert.True(left < right);
        Assert.False(left > right);
    }
    
    [Fact]
    public void Comparison_GreaterThan_ReturnsTrue()
    {
        var left = new Quantity(150m);
        var right = new Quantity(100m);
        
        Assert.True(left > right);
        Assert.False(left < right);
    }
    
    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var quantity = new Quantity(1234.5678m);
        
        var result = quantity.ToString();
        
        Assert.Equal("1,234.5678", result);
    }
    
    [Fact]
    public void Zero_ReturnsZeroQuantity()
    {
        Assert.Equal(0m, Quantity.Zero.Amount);
    }
}
