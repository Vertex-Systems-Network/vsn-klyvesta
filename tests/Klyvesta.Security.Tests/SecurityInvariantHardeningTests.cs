using Klyvesta.Domain.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klyvesta.Security.Tests;

[TestClass]
public sealed class SecurityInvariantHardeningTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 18, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void AuthenticatedFlagDoesNotAuthorizeEmptyPrincipalId()
    {
        var customerId = Guid.NewGuid();
        var principal = new SecurityPrincipal(
            Guid.Empty,
            PrincipalType.Customer,
            SecurityRole.Investor,
            customerId,
            new HashSet<string>(StringComparer.Ordinal));
        var request = HealthyRequest(SecurityAction.RequestWithdrawal, customerId);

        var decision = AuthorizationEvaluator.Evaluate(principal, request);

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual(SecurityDenialReason.AuthRequired, decision.Reason);
    }

    [TestMethod]
    public void EmptyCustomerIdentifierCannotOwnWithdrawalResource()
    {
        var principal = new SecurityPrincipal(
            Guid.NewGuid(),
            PrincipalType.Customer,
            SecurityRole.Investor,
            Guid.Empty,
            new HashSet<string>(StringComparer.Ordinal));
        var request = HealthyRequest(SecurityAction.RequestWithdrawal, Guid.Empty);

        var decision = AuthorizationEvaluator.Evaluate(principal, request);

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual(SecurityDenialReason.ResourceNotFoundOrForbidden, decision.Reason);
    }

    [TestMethod]
    public void ProtectedApprovalRejectsInjectedApproverRoleAtConstruction()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ProtectedApproval(
                Guid.NewGuid(),
                SecurityAction.ApproveFinancialCorrection,
                Guid.NewGuid(),
                SecurityRole.PlatformAdmin,
                Now.AddMinutes(5)));
    }

    [TestMethod]
    public void ProtectedApprovalRejectsUnconfiguredAction()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ProtectedApproval(
                Guid.NewGuid(),
                SecurityAction.RequestWithdrawal,
                Guid.NewGuid(),
                SecurityRole.ReconciliationApprover,
                Now.AddMinutes(5)));
    }

    [TestMethod]
    public void ProtectedApprovalRejectsEmptyApproverPrincipalId()
    {
        var makerId = Guid.NewGuid();
        var approval = new ProtectedApproval(
            Guid.NewGuid(),
            SecurityAction.ApproveFinancialCorrection,
            makerId,
            SecurityRole.ReconciliationApprover,
            Now.AddMinutes(5));
        var approver = new SecurityPrincipal(
            Guid.Empty,
            PrincipalType.Staff,
            SecurityRole.ReconciliationApprover,
            null,
            new HashSet<string>(StringComparer.Ordinal));

        var decision = approval.TryApprove(approver, Now);

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual(SecurityDenialReason.AuthRequired, decision.Reason);
        Assert.AreEqual(ProtectedApprovalState.Pending, approval.State);
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
