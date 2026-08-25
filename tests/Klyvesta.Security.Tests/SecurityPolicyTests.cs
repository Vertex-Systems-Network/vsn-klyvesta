using Klyvesta.Domain.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klyvesta.Security.Tests;

[TestClass]
public sealed class SecurityPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 17, 45, 0, TimeSpan.Zero);

    [TestMethod]
    public void Investor_CanAuthorizeOwnWithdrawalRequest()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(SecurityRole.Investor, customerId);

        var decision = AuthorizationEvaluator.Evaluate(
            principal,
            HealthyRequest(SecurityAction.RequestWithdrawal, customerId));

        Assert.IsTrue(decision.Allowed);
        Assert.AreEqual(SecurityDenialReason.None, decision.Reason);
    }

    [TestMethod]
    public void Investor_CannotAuthorizeAnotherCustomersFinancialResource()
    {
        var principal = Customer(SecurityRole.Investor, Guid.NewGuid());

        var decision = AuthorizationEvaluator.Evaluate(
            principal,
            HealthyRequest(SecurityAction.RequestWithdrawal, Guid.NewGuid()));

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual(SecurityDenialReason.ResourceNotFoundOrForbidden, decision.Reason);
    }

    [TestMethod]
    public void Entitlement_DoesNotElevateSupportRoleIntoWithdrawalAuthority()
    {
        var principal = Staff(SecurityRole.SupportL1, "withdrawal.request");

        var decision = AuthorizationEvaluator.Evaluate(
            principal,
            HealthyRequest(
                SecurityAction.RequestWithdrawal,
                Guid.NewGuid(),
                requiredEntitlement: "withdrawal.request"));

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual(SecurityDenialReason.RoleDenied, decision.Reason);
    }

    [TestMethod]
    public void AiPrincipal_CannotSubmitBrokerOrder()
    {
        var principal = Service(SecurityRole.RebalanceAgent);

        var decision = AuthorizationEvaluator.Evaluate(
            principal,
            HealthyRequest(SecurityAction.SubmitBrokerOrder));

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual(SecurityDenialReason.RoleDenied, decision.Reason);
    }

    [TestMethod]
    public void ExecutionValidator_CanReachNarrowBrokerSubmissionAuthorization()
    {
        var principal = Service(SecurityRole.ExecutionValidator);

        var decision = AuthorizationEvaluator.Evaluate(
            principal,
            HealthyRequest(SecurityAction.SubmitBrokerOrder));

        Assert.IsTrue(decision.Allowed);
    }

    [TestMethod]
    public void FinancialAction_FailsClosedWhenAccountRestricted()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(SecurityRole.Investor, customerId);
        var request = HealthyRequest(SecurityAction.RequestWithdrawal, customerId) with
        {
            AccountStatus = AccountStatus.Restricted,
        };

        var decision = AuthorizationEvaluator.Evaluate(principal, request);

        Assert.AreEqual(SecurityDenialReason.AccountRestricted, decision.Reason);
    }

    [TestMethod]
    public void FinancialAction_FailsClosedOnComplianceHold()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(SecurityRole.Investor, customerId);
        var request = HealthyRequest(SecurityAction.RequestWithdrawal, customerId) with
        {
            ComplianceHold = true,
        };

        var decision = AuthorizationEvaluator.Evaluate(principal, request);

        Assert.AreEqual(SecurityDenialReason.ComplianceHold, decision.Reason);
    }

    [TestMethod]
    public void LegalFeatureGate_IsEvaluatedServerSide()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(SecurityRole.Investor, customerId);
        var request = HealthyRequest(SecurityAction.ManageAutoMandate, customerId) with
        {
            LegalFeatureEnabled = false,
        };

        var decision = AuthorizationEvaluator.Evaluate(principal, request);

        Assert.AreEqual(SecurityDenialReason.FeatureNotLegallyAvailable, decision.Reason);
    }

    [TestMethod]
    public void AllowedRoleWithoutRequiredEntitlement_IsDenied()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(SecurityRole.Investor, customerId);

        var decision = AuthorizationEvaluator.Evaluate(
            principal,
            HealthyRequest(
                SecurityAction.ApproveAiRecommendation,
                customerId,
                requiredEntitlement: "ai.recommendations.personalized"));

        Assert.AreEqual(SecurityDenialReason.FeatureNotEntitled, decision.Reason);
    }

    [TestMethod]
    public void Withdrawal_HappyPathRequiresVerifiedBeneficiaryAndBoundStepUp()
    {
        var context = ValidWithdrawalContext();

        var decision = WithdrawalSecurityPolicy.Evaluate(context);

        Assert.IsTrue(decision.Allowed);
    }

    [TestMethod]
    public void Withdrawal_RejectsBeneficiaryOwnedByAnotherCustomer()
    {
        var context = ValidWithdrawalContext() with
        {
            Beneficiary = new WithdrawalBeneficiary(
                Guid.NewGuid(),
                Guid.NewGuid(),
                BeneficiaryVerificationStatus.Verified,
                Now.AddMinutes(-1)),
        };

        var decision = WithdrawalSecurityPolicy.Evaluate(context);

        Assert.AreEqual(SecurityDenialReason.ResourceNotFoundOrForbidden, decision.Reason);
    }

    [TestMethod]
    public void Withdrawal_RejectsUnverifiedBeneficiary()
    {
        var context = ValidWithdrawalContext();
        context = context with
        {
            Beneficiary = context.Beneficiary with
            {
                VerificationStatus = BeneficiaryVerificationStatus.Unverified,
            },
        };

        var decision = WithdrawalSecurityPolicy.Evaluate(context);

        Assert.AreEqual(SecurityDenialReason.BeneficiaryUnverified, decision.Reason);
    }

    [TestMethod]
    public void Withdrawal_EnforcesBeneficiaryCoolingOff()
    {
        var context = ValidWithdrawalContext();
        context = context with
        {
            Beneficiary = context.Beneficiary with
            {
                AvailableAfter = Now.AddMinutes(30),
            },
        };

        var decision = WithdrawalSecurityPolicy.Evaluate(context);

        Assert.AreEqual(SecurityDenialReason.BeneficiaryCoolingOff, decision.Reason);
    }

    [TestMethod]
    public void Withdrawal_RecoveryRestrictedStateBlocksHighRiskAction()
    {
        var context = ValidWithdrawalContext();
        var recovery = BuildRecoveredRestrictedState();
        context = context with { Recovery = recovery };

        var decision = WithdrawalSecurityPolicy.Evaluate(context);

        Assert.AreEqual(SecurityDenialReason.SecurityHold, decision.Reason);
    }

    [TestMethod]
    public void Withdrawal_NewOrRecoveredDeviceRestrictionBlocksHighRiskAction()
    {
        var context = ValidWithdrawalContext() with
        {
            NewOrRecoveredDeviceRestricted = true,
        };

        var decision = WithdrawalSecurityPolicy.Evaluate(context);

        Assert.AreEqual(SecurityDenialReason.SecurityHold, decision.Reason);
    }

    [TestMethod]
    public void Withdrawal_ExpiredStepUpIsDenied()
    {
        var context = ValidWithdrawalContext();
        var expired = StepUpGrant.Create(
            context.Principal.PrincipalId,
            context.SessionId,
            SecurityAction.RequestWithdrawal,
            AuthenticationStrength.StrongMfa,
            Now.AddMinutes(-10),
            Now.AddSeconds(-1));
        context = context with { StepUpGrant = expired };

        var decision = WithdrawalSecurityPolicy.Evaluate(context);

        Assert.AreEqual(SecurityDenialReason.StepUpRequired, decision.Reason);
    }

    [TestMethod]
    public void Withdrawal_StepUpForDifferentActionIsDenied()
    {
        var context = ValidWithdrawalContext();
        var wrongAction = StepUpGrant.Create(
            context.Principal.PrincipalId,
            context.SessionId,
            SecurityAction.ChangeBeneficiary,
            AuthenticationStrength.PhishingResistant,
            Now.AddMinutes(-1),
            Now.AddMinutes(4));
        context = context with { StepUpGrant = wrongAction };

        var decision = WithdrawalSecurityPolicy.Evaluate(context);

        Assert.AreEqual(SecurityDenialReason.StepUpRequired, decision.Reason);
    }

    [TestMethod]
    public void Recovery_CannotClearUntilTimeAndRiskReviewAreSatisfied()
    {
        var recovery = BuildRecoveredRestrictedState();

        var beforeTime = recovery.TryClearRestriction(Now.AddMinutes(10), riskReviewSatisfied: true);
        var withoutReview = recovery.TryClearRestriction(Now.AddHours(2), riskReviewSatisfied: false);
        var accepted = recovery.TryClearRestriction(Now.AddHours(2), riskReviewSatisfied: true);

        Assert.AreEqual(SecurityDenialReason.SecurityHold, beforeTime.Reason);
        Assert.AreEqual(SecurityDenialReason.SecurityHold, withoutReview.Reason);
        Assert.IsTrue(accepted.Allowed);
        Assert.AreEqual(RecoveryState.Normal, recovery.State);
    }

    [TestMethod]
    public void Recovery_InvalidTransitionFailsClosed()
    {
        var recovery = new RecoverySecurityState();

        var decision = recovery.PlaceSecurityHold();

        Assert.AreEqual(SecurityDenialReason.InvalidStateTransition, decision.Reason);
        Assert.AreEqual(RecoveryState.Normal, recovery.State);
    }

    [TestMethod]
    public void MakerCannotApproveOwnProtectedProposal()
    {
        var maker = Staff(SecurityRole.ReconciliationApprover);
        var approval = new ProtectedApproval(
            Guid.NewGuid(),
            SecurityAction.ApproveFinancialCorrection,
            maker.PrincipalId,
            SecurityRole.ReconciliationApprover,
            Now.AddMinutes(5));

        var decision = approval.TryApprove(maker, Now);

        Assert.AreEqual(SecurityDenialReason.MakerCheckerViolation, decision.Reason);
        Assert.AreEqual(ProtectedApprovalState.Pending, approval.State);
    }

    [TestMethod]
    public void ProtectedProposal_RejectsWrongApproverRole()
    {
        var maker = Staff(SecurityRole.ReconciliationOperator);
        var wrongApprover = Staff(SecurityRole.PlatformAdmin);
        var approval = new ProtectedApproval(
            Guid.NewGuid(),
            SecurityAction.ApproveFinancialCorrection,
            maker.PrincipalId,
            SecurityRole.ReconciliationApprover,
            Now.AddMinutes(5));

        var decision = approval.TryApprove(wrongApprover, Now);

        Assert.AreEqual(SecurityDenialReason.RoleDenied, decision.Reason);
    }

    [TestMethod]
    public void ProtectedProposal_AllowsDistinctRequiredApprover()
    {
        var maker = Staff(SecurityRole.ReconciliationOperator);
        var approver = Staff(SecurityRole.ReconciliationApprover);
        var approval = new ProtectedApproval(
            Guid.NewGuid(),
            SecurityAction.ApproveFinancialCorrection,
            maker.PrincipalId,
            SecurityRole.ReconciliationApprover,
            Now.AddMinutes(5));

        var decision = approval.TryApprove(approver, Now);

        Assert.IsTrue(decision.Allowed);
        Assert.AreEqual(ProtectedApprovalState.Approved, approval.State);
        Assert.AreEqual(approver.PrincipalId, approval.ApprovedByPrincipalId);
    }

    [TestMethod]
    public void ProtectedProposal_ExpiresClosed()
    {
        var maker = Staff(SecurityRole.ReconciliationOperator);
        var approver = Staff(SecurityRole.ReconciliationApprover);
        var approval = new ProtectedApproval(
            Guid.NewGuid(),
            SecurityAction.ApproveFinancialCorrection,
            maker.PrincipalId,
            SecurityRole.ReconciliationApprover,
            Now.AddSeconds(-1));

        var decision = approval.TryApprove(approver, Now);

        Assert.AreEqual(SecurityDenialReason.ApprovalExpired, decision.Reason);
        Assert.AreEqual(ProtectedApprovalState.Expired, approval.State);
    }

    private static SecurityPrincipal Customer(SecurityRole role, Guid customerId, params string[] entitlements) =>
        new(Guid.NewGuid(), PrincipalType.Customer, role, customerId, Entitlements(entitlements));

    private static SecurityPrincipal Staff(SecurityRole role, params string[] entitlements) =>
        new(Guid.NewGuid(), PrincipalType.Staff, role, null, Entitlements(entitlements));

    private static SecurityPrincipal Service(SecurityRole role, params string[] entitlements) =>
        new(Guid.NewGuid(), PrincipalType.Service, role, null, Entitlements(entitlements));

    private static IReadOnlySet<string> Entitlements(IEnumerable<string> values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private static AuthorizationRequest HealthyRequest(
        SecurityAction action,
        Guid? resourceCustomerId = null,
        string? requiredEntitlement = null) =>
        new(
            action,
            resourceCustomerId,
            IsAuthenticated: true,
            SessionState: SecuritySessionState.Active,
            AccountStatus: AccountStatus.Active,
            ComplianceHold: false,
            SecurityHold: false,
            RequiredEntitlement: requiredEntitlement,
            LegalFeatureEnabled: true);

    private static WithdrawalSecurityContext ValidWithdrawalContext()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(SecurityRole.Investor, customerId);
        var sessionId = Guid.NewGuid();
        var beneficiary = new WithdrawalBeneficiary(
            Guid.NewGuid(),
            customerId,
            BeneficiaryVerificationStatus.Verified,
            Now.AddMinutes(-1));
        var stepUp = StepUpGrant.Create(
            principal.PrincipalId,
            sessionId,
            SecurityAction.RequestWithdrawal,
            AuthenticationStrength.StrongMfa,
            Now.AddMinutes(-1),
            Now.AddMinutes(4));

        return new WithdrawalSecurityContext(
            principal,
            sessionId,
            customerId,
            IsAuthenticated: true,
            SessionState: SecuritySessionState.Active,
            AccountStatus: AccountStatus.Active,
            ComplianceHold: false,
            SecurityHold: false,
            NewOrRecoveredDeviceRestricted: false,
            Recovery: new RecoverySecurityState(),
            Beneficiary: beneficiary,
            StepUpGrant: stepUp,
            Now: Now);
    }

    private static RecoverySecurityState BuildRecoveredRestrictedState()
    {
        var recovery = new RecoverySecurityState();
        Assert.IsTrue(recovery.StartRecovery(Now.AddHours(-1)).Allowed);
        Assert.IsTrue(recovery.BeginIdentityReverification().Allowed);
        Assert.IsTrue(recovery.PlaceSecurityHold().Allowed);
        Assert.IsTrue(recovery.MarkRecoveredRestricted(Now, TimeSpan.FromHours(1)).Allowed);
        return recovery;
    }
}
