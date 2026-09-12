using Klyvesta.Domain.Entities;
using Klyvesta.Domain.ValueObjects;
using Klyvesta.Infrastructure.Services;
using Xunit;

namespace Klyvesta.Tests.Unit.Infrastructure;

public class ComplianceGateTests
{
    private readonly ComplianceGate _gate;
    private readonly Guid _customerId = Guid.NewGuid();

    public ComplianceGateTests()
    {
        _gate = new ComplianceGate();
    }

    [Fact]
    public void CheckRegulatoryCompliance_ValidAccount_AllowsOrder()
    {
        var accountStatus = new AccountStatus(
            _customerId,
            true, // IsActive
            false, // IsUnderHold
            new List<string>(), // RestrictedSymbols
            DateTime.UtcNow.AddDays(-30), // KycVerifiedAt
            true // AmlCleared
        );

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

        var result = _gate.CheckRegulatoryCompliance(order, accountStatus);
        
        Assert.True(result.Allowed);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void CheckRegulatoryCompliance_AccountOnHold_DeniesOrder()
    {
        var accountStatus = new AccountStatus(
            _customerId,
            true, // IsActive
            true, // IsUnderHold
            new List<string>(), // RestrictedSymbols
            DateTime.UtcNow.AddDays(-30), // KycVerifiedAt
            true // AmlCleared
        );

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

        var result = _gate.CheckRegulatoryCompliance(order, accountStatus);
        
        Assert.False(result.Allowed);
        Assert.Contains("hold", result.Reason?.ToLower());
    }

    [Fact]
    public void CheckRegulatoryCompliance_RestrictedSymbol_DeniesOrder()
    {
        var accountStatus = new AccountStatus(
            _customerId,
            true, // IsActive
            false, // IsUnderHold
            new List<string> { "PSX100", "OGDC" }, // RestrictedSymbols
            DateTime.UtcNow.AddDays(-30), // KycVerifiedAt
            true // AmlCleared
        );

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

        var result = _gate.CheckRegulatoryCompliance(order, accountStatus);
        
        Assert.False(result.Allowed);
        Assert.Contains("restricted", result.Reason?.ToLower());
    }

    [Fact]
    public void CheckRegulatoryCompliance_KycNotVerified_DeniesOrder()
    {
        var accountStatus = new AccountStatus(
            _customerId,
            true, // IsActive
            false, // IsUnderHold
            new List<string>(), // RestrictedSymbols
            null, // KycVerifiedAt - not verified
            true // AmlCleared
        );

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

        var result = _gate.CheckRegulatoryCompliance(order, accountStatus);
        
        Assert.False(result.Allowed);
        Assert.Contains("kyc", result.Reason?.ToLower());
    }

    [Fact]
    public void CheckRegulatoryCompliance_AmlNotCleared_DeniesOrder()
    {
        var accountStatus = new AccountStatus(
            _customerId,
            true, // IsActive
            false, // IsUnderHold
            new List<string>(), // RestrictedSymbols
            DateTime.UtcNow.AddDays(-30), // KycVerifiedAt
            false // AmlCleared
        );

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

        var result = _gate.CheckRegulatoryCompliance(order, accountStatus);
        
        Assert.False(result.Allowed);
        Assert.Contains("aml", result.Reason?.ToLower());
    }

    [Fact]
    public void VerifyMandate_ValidMandate_AllowsAutoTrading()
    {
        var mandate = new AiTradingMandate(
            _customerId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(364),
            new List<string> { "PSX100", "OGDC", "PPL" },
            Money.FromDecimal(1000000m, "PKR"),
            10.0m,
            true, // IsActive
            DateTime.UtcNow, // CreatedAt
            _customerId // SignedBy
        );

        var result = _gate.VerifyMandate(mandate, _customerId, "PSX100");
        
        Assert.True(result.Valid);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void VerifyMandate_ExpiredMandate_DeniesAutoTrading()
    {
        var mandate = new AiTradingMandate(
            _customerId,
            DateTime.UtcNow.AddDays(-400),
            DateTime.UtcNow.AddDays(-35),
            new List<string> { "PSX100" },
            Money.FromDecimal(500000m, "PKR"),
            5.0m,
            true, // IsActive
            DateTime.UtcNow.AddDays(-400), // CreatedAt
            _customerId // SignedBy
        );

        var result = _gate.VerifyMandate(mandate, _customerId, "PSX100");
        
        Assert.False(result.Valid);
        Assert.Contains("expired", result.Reason?.ToLower());
    }

    [Fact]
    public void VerifyMandate_InactiveMandate_DeniesAutoTrading()
    {
        var mandate = new AiTradingMandate(
            _customerId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(364),
            new List<string> { "PSX100" },
            Money.FromDecimal(500000m, "PKR"),
            5.0m,
            false, // IsActive - revoked
            DateTime.UtcNow.AddDays(-1), // CreatedAt
            _customerId // SignedBy
        );

        var result = _gate.VerifyMandate(mandate, _customerId, "PSX100");
        
        Assert.False(result.Valid);
        Assert.Contains("inactive", result.Reason?.ToLower());
    }

    [Fact]
    public void VerifyMandate_SymbolNotInMandate_DeniesAutoTrading()
    {
        var mandate = new AiTradingMandate(
            _customerId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(364),
            new List<string> { "PSX100", "OGDC" },
            Money.FromDecimal(1000000m, "PKR"),
            10.0m,
            true, // IsActive
            DateTime.UtcNow, // CreatedAt
            _customerId // SignedBy
        );

        var result = _gate.VerifyMandate(mandate, _customerId, "FERT");
        
        Assert.False(result.Valid);
        Assert.Contains("not authorized", result.Reason?.ToLower());
    }

    [Fact]
    public void VerifyMandate_WrongCustomer_DeniesAutoTrading()
    {
        var otherCustomerId = Guid.NewGuid();
        var mandate = new AiTradingMandate(
            otherCustomerId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(364),
            new List<string> { "PSX100" },
            Money.FromDecimal(1000000m, "PKR"),
            10.0m,
            true, // IsActive
            DateTime.UtcNow, // CreatedAt
            otherCustomerId // SignedBy
        );

        var result = _gate.VerifyMandate(mandate, _customerId, "PSX100");
        
        Assert.False(result.Valid);
        Assert.Contains("customer", result.Reason?.ToLower());
    }

    [Fact]
    public void FullComplianceCheck_AllChecksPass_ReturnsSuccess()
    {
        var accountStatus = new AccountStatus(
            _customerId,
            true, // IsActive
            false, // IsUnderHold
            new List<string>(), // RestrictedSymbols
            DateTime.UtcNow.AddDays(-30), // KycVerifiedAt
            true // AmlCleared
        );

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

        var mandate = (AiTradingMandate?)null;

        var result = _gate.FullComplianceCheck(order, accountStatus, mandate);
        
        Assert.True(result.Allowed);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void FullComplianceCheck_MultipleFailures_ReturnsAllFailures()
    {
        var accountStatus = new AccountStatus(
            _customerId,
            true, // IsActive
            true, // IsUnderHold
            new List<string> { "PSX100" }, // RestrictedSymbols
            null, // KycVerifiedAt
            false // AmlCleared
        );

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
            true // Auto trading without mandate
        );

        var mandate = (AiTradingMandate?)null;

        var result = _gate.FullComplianceCheck(order, accountStatus, mandate);
        
        Assert.False(result.Allowed);
        Assert.NotEmpty(result.Failures);
        Assert.Contains(result.Failures, f => f.ToLower().Contains("hold") || f.ToLower().Contains("kyc") || f.ToLower().Contains("aml") || f.ToLower().Contains("mandate"));
    }

    [Fact]
    public void CheckPatternMonitoring_SuspiciousPattern_FlagsForReview()
    {
        var recentOrders = new List<OrderIntent>
        {
            new OrderIntent(_customerId, Symbol.FromString("PSX100"), Side.Buy, Quantity.FromDecimal(100m), Money.FromDecimal(50000m, "PKR"), OrderType.Limit, TimeInForce.Day, null, Channel.Web, false),
            new OrderIntent(_customerId, Symbol.FromString("PSX100"), Side.Sell, Quantity.FromDecimal(100m), Money.FromDecimal(51000m, "PKR"), OrderType.Limit, TimeInForce.Day, null, Channel.Web, false),
            new OrderIntent(_customerId, Symbol.FromString("PSX100"), Side.Buy, Quantity.FromDecimal(100m), Money.FromDecimal(50000m, "PKR"), OrderType.Limit, TimeInForce.Day, null, Channel.Web, false),
            new OrderIntent(_customerId, Symbol.FromString("PSX100"), Side.Sell, Quantity.FromDecimal(100m), Money.FromDecimal(51000m, "PKR"), OrderType.Limit, TimeInForce.Day, null, Channel.Web, false),
            new OrderIntent(_customerId, Symbol.FromString("PSX100"), Side.Buy, Quantity.FromDecimal(100m), Money.FromDecimal(50000m, "PKR"), OrderType.Limit, TimeInForce.Day, null, Channel.Web, false),
        ];

        var result = _gate.CheckPatternMonitoring(recentOrders, TimeSpan.FromHours(1));
        
        Assert.True(result.RequiresReview);
        Assert.Contains("pattern", result.Reason?.ToLower());
    }

    [Fact]
    public void CheckPatternMonitoring_NormalActivity_NoFlag()
    {
        var recentOrders = new List<OrderIntent>
        {
            new OrderIntent(_customerId, Symbol.FromString("PSX100"), Side.Buy, Quantity.FromDecimal(100m), Money.FromDecimal(50000m, "PKR"), OrderType.Limit, TimeInForce.Day, null, Channel.Web, false),
        };

        var result = _gate.CheckPatternMonitoring(recentOrders, TimeSpan.FromHours(1));
        
        Assert.False(result.RequiresReview);
        Assert.Null(result.Reason);
    }
}
