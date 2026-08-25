using Klyvesta.Domain.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klyvesta.Security.Tests;

[TestClass]
public sealed class PrincipalTypeBoundaryTests
{
    [TestMethod]
    public void StaffPrincipalCannotMasqueradeAsInvestorForWithdrawal()
    {
        var customerId = Guid.NewGuid();
        var principal = new SecurityPrincipal(
            Guid.NewGuid(),
            PrincipalType.Staff,
            SecurityRole.Investor,
            customerId,
            new HashSet<string>(StringComparer.Ordinal));
        var request = HealthyRequest(SecurityAction.RequestWithdrawal, customerId);

        var decision = AuthorizationEvaluator.Evaluate(principal, request);

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual(SecurityDenialReason.RoleDenied, decision.Reason);
    }

    [TestMethod]
    public void CustomerPrincipalCannotMasqueradeAsExecutionValidator()
    {
        var principal = new SecurityPrincipal(
            Guid.NewGuid(),
            PrincipalType.Customer,
            SecurityRole.ExecutionValidator,
            Guid.NewGuid(),
            new HashSet<string>(StringComparer.Ordinal));
        var request = HealthyRequest(SecurityAction.SubmitBrokerOrder);

        var decision = AuthorizationEvaluator.Evaluate(principal, request);

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual(SecurityDenialReason.RoleDenied, decision.Reason);
    }

    private static AuthorizationRequest HealthyRequest(SecurityAction action, Guid? resourceCustomerId = null) =>
        new(
            action,
            resourceCustomerId,
            IsAuthenticated: true,
            SessionState: SecuritySessionState.Active,
            AccountStatus: AccountStatus.Active,
            ComplianceHold: false,
            SecurityHold: false);
}
