using System;

namespace Klyvesta.Domain.Compliance;

/// <summary>
/// Compliance decision outcomes
/// </summary>
public enum ComplianceDecisionType
{
    Approve = 0,
    Deny = 1,
    RequireManualReview = 2
}
