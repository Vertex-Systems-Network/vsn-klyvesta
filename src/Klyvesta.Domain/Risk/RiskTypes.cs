using System;

namespace Klyvesta.Domain.Risk;

/// <summary>
/// Risk decision outcomes
/// </summary>
public enum RiskDecisionType
{
    Approve = 0,
    Deny = 1,
    RequireManualReview = 2
}

/// <summary>
/// Overall risk level classification
/// </summary>
public enum RiskLevel
{
    Conservative = 0,
    Moderate = 1,
    Aggressive = 2,
    VeryAggressive = 3
}

/// <summary>
/// Policy for responding to drawdown limits being approached
/// </summary>
public enum DrawdownResponsePolicy
{
    NotifyOnly = 0,
    ReduceExposure = 1,
    HaltTrading = 2,
    LiquidatePosition = 3
}
