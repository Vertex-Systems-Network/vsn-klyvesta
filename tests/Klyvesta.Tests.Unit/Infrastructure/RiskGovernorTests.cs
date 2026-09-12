using Klyvesta.Domain.Entities;
using Klyvesta.Domain.ValueObjects;
using Klyvesta.Infrastructure.Services;
using Xunit;

namespace Klyvesta.Tests.Unit.Infrastructure;

public class RiskGovernorTests
{
    private readonly RiskGovernor _governor;
    private readonly Guid _customerId = Guid.NewGuid();

    public RiskGovernorTests()
    {
        _governor = new RiskGovernor();
    }

    [Fact]
    public void CheckOrderValueLimit_WithinLimit_AllowsOrder()
    {
        var order = new OrderIntent(
            _customerId,
            Symbol.FromString("PSX100"),
            Side.Buy,
            Quantity.FromDecimal(100m),
            Money.FromDecimal(50000m, "PKR"),
            OrderType.Limit,
            TimeInForce.Day,
            null,
            Channel.Web,
            false
        );

        var result = _governor.CheckOrderValueLimit(order);
        
        Assert.True(result.Allowed);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void CheckOrderValueLimit_ExceedsLimit_DeniesOrder()
    {
        var order = new OrderIntent(
            _customerId,
            Symbol.FromString("PSX100"),
            Side.Buy,
            Quantity.FromDecimal(10000m),
            Money.FromDecimal(5000000m, "PKR"),
            OrderType.Limit,
            TimeInForce.Day,
            null,
            Channel.Web,
            false
        );

        var result = _governor.CheckOrderValueLimit(order);
        
        Assert.False(result.Allowed);
        Assert.Contains("order value", result.Reason?.ToLower());
    }

    [Fact]
    public void CheckConcentrationLimit_DiversifiedPortfolio_AllowsOrder()
    {
        var currentHoldings = new Dictionary<string, Quantity>
        {
            { "PSX100", Quantity.FromDecimal(1000m) },
            { "OGDC", Quantity.FromDecimal(500m) },
            { "PPL", Quantity.FromDecimal(800m) }
        };

        var order = new OrderIntent(
            _customerId,
            Symbol.FromString("FERT"),
            Side.Buy,
            Quantity.FromDecimal(200m),
            Money.FromDecimal(30000m, "PKR"),
            OrderType.Limit,
            TimeInForce.Day,
            null,
            Channel.Web,
            false
        );

        var result = _governor.CheckConcentrationLimit(order, currentHoldings);
        
        Assert.True(result.Allowed);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void CheckConcentrationLimit_SingleStockTooHigh_DeniesOrder()
    {
        var currentHoldings = new Dictionary<string, Quantity>
        {
            { "PSX100", Quantity.FromDecimal(10000m) }
        };

        var order = new OrderIntent(
            _customerId,
            Symbol.FromString("PSX100"),
            Side.Buy,
            Quantity.FromDecimal(5000m),
            Money.FromDecimal(750000m, "PKR"),
            OrderType.Limit,
            TimeInForce.Day,
            null,
            Channel.Web,
            false
        );

        var result = _governor.CheckConcentrationLimit(order, currentHoldings);
        
        Assert.False(result.Allowed);
        Assert.Contains("concentration", result.Reason?.ToLower());
    }

    [Fact]
    public void VerifyAutoTradingEligible_WithMandate_AllowsAuto()
    {
        var mandate = new AiTradingMandate(
            _customerId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(364),
            new List<string> { "PSX100", "OGDC", "PPL" },
            Money.FromDecimal(1000000m, "PKR"),
            10.0m
        );

        var result = _governor.VerifyAutoTradingEligible(_customerId, mandate, true);
        
        Assert.True(result.Allowed);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void VerifyAutoTradingEligible_NoMandate_DeniesAuto()
    {
        var result = _governor.VerifyAutoTradingEligible(_customerId, null, true);
        
        Assert.False(result.Allowed);
        Assert.Contains("mandate", result.Reason?.ToLower());
    }

    [Fact]
    public void VerifyAutoTradingEligible_MandateExpired_DeniesAuto()
    {
        var mandate = new AiTradingMandate(
            _customerId,
            DateTime.UtcNow.AddDays(-400),
            DateTime.UtcNow.AddDays(-35),
            new List<string> { "PSX100" },
            Money.FromDecimal(500000m, "PKR"),
            5.0m
        );

        var result = _governor.VerifyAutoTradingEligible(_customerId, mandate, true);
        
        Assert.False(result.Allowed);
        Assert.Contains("expired", result.Reason?.ToLower());
    }

    [Fact]
    public void VerifyAutoTradingEligible_SymbolNotAllowed_DeniesAuto()
    {
        var mandate = new AiTradingMandate(
            _customerId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(364),
            new List<string> { "PSX100", "OGDC" },
            Money.FromDecimal(1000000m, "PKR"),
            10.0m
        );

        var result = _governor.VerifyAutoTradingEligible(_customerId, mandate, true, "FERT");
        
        Assert.False(result.Allowed);
        Assert.Contains("not allowed", result.Reason?.ToLower());
    }

    [Fact]
    public void CheckExposureLimit_WithinDailyLimit_AllowsOrder()
    {
        var todayExecutions = new List<Execution>();
        
        var order = new OrderIntent(
            _customerId,
            Symbol.FromString("PSX100"),
            Side.Buy,
            Quantity.FromDecimal(100m),
            Money.FromDecimal(50000m, "PKR"),
            OrderType.Limit,
            TimeInForce.Day,
            null,
            Channel.Web,
            false
        );

        var result = _governor.CheckExposureLimit(order, todayExecutions);
        
        Assert.True(result.Allowed);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void CheckExposureLimit_ExceedsDailyLimit_DeniesOrder()
    {
        var existingExecution = new Execution(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Symbol.FromString("PSX100"),
            Side.Buy,
            Quantity.FromDecimal(1000m),
            Money.FromDecimal(150000m, "PKR"),
            DateTime.UtcNow,
            "BROKER_REF_001"
        );

        var todayExecutions = new List<Execution> { existingExecution };
        
        var order = new OrderIntent(
            _customerId,
            Symbol.FromString("PSX100"),
            Side.Buy,
            Quantity.FromDecimal(1000m),
            Money.FromDecimal(150000m, "PKR"),
            OrderType.Limit,
            TimeInForce.Day,
            null,
            Channel.Web,
            false
        );

        var result = _governor.CheckExposureLimit(order, todayExecutions);
        
        Assert.False(result.Allowed);
        Assert.Contains("exposure", result.Reason?.ToLower());
    }

    [Fact]
    public void FullRiskCheck_AllChecksPass_ReturnsSuccess()
    {
        var order = new OrderIntent(
            _customerId,
            Symbol.FromString("PSX100"),
            Side.Buy,
            Quantity.FromDecimal(100m),
            Money.FromDecimal(50000m, "PKR"),
            OrderType.Limit,
            TimeInForce.Day,
            null,
            Channel.Web,
            false
        );

        var currentHoldings = new Dictionary<string, Quantity>();
        var todayExecutions = new List<Execution>();
        var mandate = (AiTradingMandate?)null;

        var result = _governor.FullRiskCheck(order, currentHoldings, todayExecutions, mandate);
        
        Assert.True(result.Allowed);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void FullRiskCheck_MultipleFailures_ReturnsAllFailures()
    {
        var order = new OrderIntent(
            _customerId,
            Symbol.FromString("PSX100"),
            Side.Buy,
            Quantity.FromDecimal(10000m),
            Money.FromDecimal(5000000m, "PKR"),
            OrderType.Limit,
            TimeInForce.Day,
            null,
            Channel.Web,
            true // Auto trading without mandate
        );

        var currentHoldings = new Dictionary<string, Quantity>();
        var todayExecutions = new List<Execution>();
        var mandate = (AiTradingMandate?)null;

        var result = _governor.FullRiskCheck(order, currentHoldings, todayExecutions, mandate);
        
        Assert.False(result.Allowed);
        Assert.NotEmpty(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("order value") || f.Contains("mandate"));
    }
}
