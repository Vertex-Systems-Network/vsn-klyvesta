using Klyvesta.Domain.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klyvesta.Security.Tests;

[TestClass]
public sealed class WithdrawalLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 20, 0, 0, TimeSpan.Zero);
    private const string DestinationHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [TestMethod]
    public void BeneficiaryCannotActivateBeforeVerification()
    {
        var beneficiary = CreatePendingBeneficiary(Guid.NewGuid());

        var decision = beneficiary.Activate(Now);

        Assert.AreEqual(SecurityDenialReason.InvalidStateTransition, decision.Reason);
        Assert.AreEqual(BeneficiaryLifecycleState.PendingVerification, beneficiary.State);
    }

    [TestMethod]
    public void BeneficiaryCoolingOffMustFinishBeforeActivation()
    {
        var customerId = Guid.NewGuid();
        var beneficiary = CreatePendingBeneficiary(customerId);

        Assert.IsTrue(beneficiary.Verify("verification-1", Now, TimeSpan.FromHours(1)).Allowed);
        Assert.AreEqual(
            SecurityDenialReason.BeneficiaryCoolingOff,
            beneficiary.EvaluateForWithdrawal(customerId, Now.AddMinutes(30)).Reason);
        Assert.AreEqual(
            SecurityDenialReason.BeneficiaryCoolingOff,
            beneficiary.Activate(Now.AddMinutes(30)).Reason);
        Assert.IsTrue(beneficiary.Activate(Now.AddHours(1)).Allowed);
        Assert.IsTrue(beneficiary.EvaluateForWithdrawal(customerId, Now.AddHours(1)).Allowed);
    }

    [TestMethod]
    public void BeneficiaryOwnershipIsFailClosed()
    {
        var beneficiary = CreateActiveBeneficiary(Guid.NewGuid());

        var decision = beneficiary.EvaluateForWithdrawal(Guid.NewGuid(), Now.AddHours(2));

        Assert.AreEqual(SecurityDenialReason.ResourceNotFoundOrForbidden, decision.Reason);
    }

    [TestMethod]
    public void BlockedBeneficiaryCannotBeUsedForWithdrawal()
    {
        var customerId = Guid.NewGuid();
        var beneficiary = CreateActiveBeneficiary(customerId);

        Assert.IsTrue(beneficiary.Block("fraud_review").Allowed);

        var decision = beneficiary.EvaluateForWithdrawal(customerId, Now.AddHours(2));

        Assert.AreEqual(SecurityDenialReason.BeneficiaryUnavailable, decision.Reason);
        Assert.AreEqual("fraud_review", beneficiary.BlockReason);
    }

    [TestMethod]
    public void NullDestinationHashFailsClosed()
    {
        AssertArgumentException(() => WithdrawalTransactionData.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "PKR",
            null!));
    }

    [TestMethod]
    public void SignificantTransactionHashChangesWhenAmountChanges()
    {
        var withdrawalId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var beneficiaryVersionId = Guid.NewGuid();
        var first = Transaction(withdrawalId, customerId, beneficiaryVersionId, 1000m);
        var changed = Transaction(withdrawalId, customerId, beneficiaryVersionId, 1001m);

        Assert.AreNotEqual(first.DataHash, changed.DataHash);
    }

    [TestMethod]
    public void SignificantTransactionHashChangesWhenBeneficiaryVersionChanges()
    {
        var withdrawalId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var first = Transaction(withdrawalId, customerId, Guid.NewGuid(), 1000m);
        var changed = Transaction(withdrawalId, customerId, Guid.NewGuid(), 1000m);

        Assert.AreNotEqual(first.DataHash, changed.DataHash);
    }

    [TestMethod]
    public void AuthorizationSnapshotRejectsCustomerPrincipalWithWrongRole()
    {
        var customerId = Guid.NewGuid();
        var principal = new SecurityPrincipal(
            Guid.NewGuid(),
            PrincipalType.Customer,
            SecurityRole.SupportL1,
            customerId,
            new HashSet<string>(StringComparer.Ordinal));
        var sessionId = Guid.NewGuid();
        var data = Transaction(Guid.NewGuid(), customerId, Guid.NewGuid(), 5000m);
        var stepUp = StepUpGrant.Create(
            principal.PrincipalId,
            sessionId,
            SecurityAction.RequestWithdrawal,
            AuthenticationStrength.PhishingResistant,
            Now.AddMinutes(-1),
            Now.AddMinutes(4));

        var attempt = WithdrawalAuthorizationSnapshot.TryCreate(
            Guid.NewGuid(),
            data,
            principal,
            sessionId,
            stepUp,
            Now,
            TimeSpan.FromMinutes(2));

        Assert.AreEqual(SecurityDenialReason.RoleDenied, attempt.Decision.Reason);
        Assert.IsNull(attempt.Snapshot);
    }

    [TestMethod]
    public void AuthorizationSnapshotRequiresOperationBoundStepUp()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(customerId);
        var sessionId = Guid.NewGuid();
        var data = Transaction(Guid.NewGuid(), customerId, Guid.NewGuid(), 5000m);
        var wrongAction = StepUpGrant.Create(
            principal.PrincipalId,
            sessionId,
            SecurityAction.ChangeBeneficiary,
            AuthenticationStrength.PhishingResistant,
            Now.AddMinutes(-1),
            Now.AddMinutes(4));

        var attempt = WithdrawalAuthorizationSnapshot.TryCreate(
            Guid.NewGuid(),
            data,
            principal,
            sessionId,
            wrongAction,
            Now,
            TimeSpan.FromMinutes(2));

        Assert.AreEqual(SecurityDenialReason.StepUpRequired, attempt.Decision.Reason);
        Assert.IsNull(attempt.Snapshot);
    }

    [TestMethod]
    public void AuthorizationSnapshotCannotOutliveStepUpGrant()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(customerId);
        var sessionId = Guid.NewGuid();
        var data = Transaction(Guid.NewGuid(), customerId, Guid.NewGuid(), 5000m);
        var stepUp = StepUpGrant.Create(
            principal.PrincipalId,
            sessionId,
            SecurityAction.RequestWithdrawal,
            AuthenticationStrength.PhishingResistant,
            Now.AddMinutes(-1),
            Now.AddSeconds(30));
        var attempt = WithdrawalAuthorizationSnapshot.TryCreate(
            Guid.NewGuid(),
            data,
            principal,
            sessionId,
            stepUp,
            Now,
            TimeSpan.FromMinutes(10));
        var snapshot = RequiredSnapshot(attempt);

        Assert.AreEqual(stepUp.ExpiresAt, snapshot.ExpiresAt);
        Assert.AreEqual(
            SecurityDenialReason.TransactionAuthorizationInvalid,
            snapshot.ValidateFor(data, principal.PrincipalId, sessionId, Now.AddSeconds(30)).Reason);
    }

    [TestMethod]
    public void AuthorizationSnapshotCannotAuthorizeModifiedTransactionData()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(customerId);
        var sessionId = Guid.NewGuid();
        var withdrawalId = Guid.NewGuid();
        var beneficiaryVersionId = Guid.NewGuid();
        var original = Transaction(withdrawalId, customerId, beneficiaryVersionId, 5000m);
        var changed = Transaction(withdrawalId, customerId, beneficiaryVersionId, 5001m);
        var snapshot = CreateSnapshot(original, principal, sessionId);

        var decision = snapshot.ValidateFor(changed, principal.PrincipalId, sessionId, Now.AddSeconds(30));

        Assert.AreEqual(SecurityDenialReason.TransactionAuthorizationInvalid, decision.Reason);
    }

    [TestMethod]
    public void AuthorizationSnapshotIsBoundToSessionAndExpiry()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(customerId);
        var sessionId = Guid.NewGuid();
        var data = Transaction(Guid.NewGuid(), customerId, Guid.NewGuid(), 5000m);
        var snapshot = CreateSnapshot(data, principal, sessionId);

        Assert.AreEqual(
            SecurityDenialReason.TransactionAuthorizationInvalid,
            snapshot.ValidateFor(data, principal.PrincipalId, Guid.NewGuid(), Now.AddSeconds(30)).Reason);
        Assert.AreEqual(
            SecurityDenialReason.TransactionAuthorizationInvalid,
            snapshot.ValidateFor(data, principal.PrincipalId, sessionId, Now.AddMinutes(2)).Reason);
    }

    [TestMethod]
    public void WithdrawalCreationRequiresExactActiveBeneficiaryVersion()
    {
        var customerId = Guid.NewGuid();
        var beneficiary = CreateActiveBeneficiary(customerId);
        var data = Transaction(Guid.NewGuid(), customerId, Guid.NewGuid(), 1000m);

        var attempt = WithdrawalRequestLifecycle.TryCreate(data, beneficiary, Guid.NewGuid(), Now);

        Assert.AreEqual(SecurityDenialReason.ResourceNotFoundOrForbidden, attempt.Decision.Reason);
        Assert.IsNull(attempt.Lifecycle);
    }

    [TestMethod]
    public void WithdrawalCreationRejectsBeneficiaryStillInCoolingOff()
    {
        var customerId = Guid.NewGuid();
        var beneficiary = CreatePendingBeneficiary(customerId);
        Assert.IsTrue(beneficiary.Verify("verification-1", Now, TimeSpan.FromHours(1)).Allowed);
        var data = Transaction(Guid.NewGuid(), customerId, beneficiary.VersionId, 1000m);

        var attempt = WithdrawalRequestLifecycle.TryCreate(data, beneficiary, Guid.NewGuid(), Now.AddMinutes(1));

        Assert.AreEqual(SecurityDenialReason.BeneficiaryCoolingOff, attempt.Decision.Reason);
        Assert.IsNull(attempt.Lifecycle);
    }

    [TestMethod]
    public void WithdrawalCannotSkipSecurityCheck()
    {
        var lifecycle = CreateLifecycle();

        var decision = lifecycle.PassSecurityCheck(Now.AddSeconds(1));

        Assert.AreEqual(SecurityDenialReason.InvalidStateTransition, decision.Reason);
        Assert.AreEqual(WithdrawalLifecycleState.Requested, lifecycle.State);
    }

    [TestMethod]
    public void SecurityHoldRequiresExplicitResumeBeforePolicyCheck()
    {
        var lifecycle = CreateLifecycle();

        Assert.IsTrue(lifecycle.BeginSecurityCheck(Now.AddSeconds(1)).Allowed);
        Assert.IsTrue(lifecycle.PlaceSecurityHold("new_device_review", Now.AddSeconds(2)).Allowed);
        Assert.AreEqual(
            SecurityDenialReason.InvalidStateTransition,
            lifecycle.PassSecurityCheck(Now.AddSeconds(3)).Reason);
        Assert.IsTrue(lifecycle.ResumeSecurityCheck(Now.AddSeconds(4)).Allowed);
        Assert.IsTrue(lifecycle.PassSecurityCheck(Now.AddSeconds(5)).Allowed);
        Assert.AreEqual(WithdrawalLifecycleState.PolicyCheck, lifecycle.State);
    }

    [TestMethod]
    public void WithdrawalRejectsBackdatedLifecycleTransitions()
    {
        var lifecycle = CreateLifecycle();
        Assert.IsTrue(lifecycle.BeginSecurityCheck(Now.AddSeconds(2)).Allowed);

        var decision = lifecycle.PassSecurityCheck(Now.AddSeconds(1));

        Assert.AreEqual(SecurityDenialReason.InvalidStateTransition, decision.Reason);
        Assert.AreEqual(WithdrawalLifecycleState.SecurityCheck, lifecycle.State);
    }

    [TestMethod]
    public void WithdrawalHappyPathRequiresAuthorizationBeforeSubmission()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(customerId);
        var sessionId = Guid.NewGuid();
        var beneficiary = CreateActiveBeneficiary(customerId);
        var data = Transaction(Guid.NewGuid(), customerId, beneficiary.VersionId, 2500m);
        var lifecycle = RequiredLifecycle(
            WithdrawalRequestLifecycle.TryCreate(data, beneficiary, principal.PrincipalId, Now));

        Assert.IsTrue(lifecycle.BeginSecurityCheck(Now.AddSeconds(1)).Allowed);
        Assert.IsTrue(lifecycle.PassSecurityCheck(Now.AddSeconds(2)).Allowed);
        Assert.IsTrue(lifecycle.PassPolicyCheck(false, Now.AddSeconds(3), TimeSpan.FromMinutes(5)).Allowed);
        Assert.AreEqual(
            SecurityDenialReason.TransactionAuthorizationInvalid,
            lifecycle.PrepareSubmission(
                CreateSnapshot(data, principal, sessionId),
                principal.PrincipalId,
                Guid.NewGuid(),
                Now.AddSeconds(4)).Reason);

        var snapshot = CreateSnapshot(data, principal, sessionId);
        Assert.IsTrue(lifecycle.PrepareSubmission(snapshot, principal.PrincipalId, sessionId, Now.AddSeconds(4)).Allowed);
        Assert.IsTrue(lifecycle.MarkSubmitted("provider-ref-1", Now.AddSeconds(5)).Allowed);
        Assert.IsTrue(lifecycle.MarkProcessing(Now.AddSeconds(6)).Allowed);
        Assert.IsTrue(lifecycle.MarkCompleted("provider-settlement-1", Now.AddSeconds(7)).Allowed);
        Assert.AreEqual(WithdrawalLifecycleState.Completed, lifecycle.State);
        Assert.AreEqual("provider-settlement-1", lifecycle.OutcomeEvidenceReference);
    }

    [TestMethod]
    public void WithdrawalAuthorizationPrincipalMustMatchOriginalRequester()
    {
        var customerId = Guid.NewGuid();
        var requester = Customer(customerId);
        var otherPrincipal = Customer(customerId);
        var sessionId = Guid.NewGuid();
        var beneficiary = CreateActiveBeneficiary(customerId);
        var data = Transaction(Guid.NewGuid(), customerId, beneficiary.VersionId, 2500m);
        var lifecycle = RequiredLifecycle(
            WithdrawalRequestLifecycle.TryCreate(data, beneficiary, requester.PrincipalId, Now));
        Assert.IsTrue(lifecycle.BeginSecurityCheck(Now.AddSeconds(1)).Allowed);
        Assert.IsTrue(lifecycle.PassSecurityCheck(Now.AddSeconds(2)).Allowed);
        Assert.IsTrue(lifecycle.PassPolicyCheck(false, Now.AddSeconds(3), TimeSpan.FromMinutes(5)).Allowed);
        var snapshot = CreateSnapshot(data, otherPrincipal, sessionId);

        var decision = lifecycle.PrepareSubmission(
            snapshot,
            otherPrincipal.PrincipalId,
            sessionId,
            Now.AddSeconds(4));

        Assert.AreEqual(SecurityDenialReason.TransactionAuthorizationInvalid, decision.Reason);
        Assert.AreEqual(WithdrawalLifecycleState.Approved, lifecycle.State);
    }

    [TestMethod]
    public void ProtectedWithdrawalApprovalRequiresComplianceOfficer()
    {
        var lifecycle = CreateLifecycle();
        Assert.IsTrue(lifecycle.BeginSecurityCheck(Now.AddSeconds(1)).Allowed);
        Assert.IsTrue(lifecycle.PassSecurityCheck(Now.AddSeconds(2)).Allowed);
        Assert.IsTrue(lifecycle.PassPolicyCheck(true, Now.AddSeconds(3), TimeSpan.FromMinutes(5)).Allowed);

        var support = Staff(SecurityRole.SupportL2);
        var denied = lifecycle.TryApprove(support, Now.AddSeconds(4));
        var compliance = Staff(SecurityRole.ComplianceOfficer);
        var approved = lifecycle.TryApprove(compliance, Now.AddSeconds(5));

        Assert.AreEqual(SecurityDenialReason.RoleDenied, denied.Reason);
        Assert.IsTrue(approved.Allowed);
        Assert.AreEqual(WithdrawalLifecycleState.Approved, lifecycle.State);
        Assert.AreEqual(compliance.PrincipalId, lifecycle.ApprovedByPrincipalId);
    }

    [TestMethod]
    public void WithdrawalMakerCannotApproveOwnProtectedRequest()
    {
        var requesterId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var beneficiary = CreateActiveBeneficiary(customerId);
        var data = Transaction(Guid.NewGuid(), customerId, beneficiary.VersionId, 2500m);
        var lifecycle = RequiredLifecycle(
            WithdrawalRequestLifecycle.TryCreate(data, beneficiary, requesterId, Now));
        Assert.IsTrue(lifecycle.BeginSecurityCheck(Now.AddSeconds(1)).Allowed);
        Assert.IsTrue(lifecycle.PassSecurityCheck(Now.AddSeconds(2)).Allowed);
        Assert.IsTrue(lifecycle.PassPolicyCheck(true, Now.AddSeconds(3), TimeSpan.FromMinutes(5)).Allowed);
        var spoofedApprover = new SecurityPrincipal(
            requesterId,
            PrincipalType.Staff,
            SecurityRole.ComplianceOfficer,
            null,
            new HashSet<string>(StringComparer.Ordinal));

        var decision = lifecycle.TryApprove(spoofedApprover, Now.AddSeconds(4));

        Assert.AreEqual(SecurityDenialReason.MakerCheckerViolation, decision.Reason);
        Assert.AreEqual(WithdrawalLifecycleState.ApprovalPending, lifecycle.State);
    }

    [TestMethod]
    public void UnknownStatePreventsBlindResubmissionAndRequiresResolutionEvidence()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(customerId);
        var sessionId = Guid.NewGuid();
        var beneficiary = CreateActiveBeneficiary(customerId);
        var data = Transaction(Guid.NewGuid(), customerId, beneficiary.VersionId, 2500m);
        var lifecycle = RequiredLifecycle(
            WithdrawalRequestLifecycle.TryCreate(data, beneficiary, principal.PrincipalId, Now));
        Assert.IsTrue(lifecycle.BeginSecurityCheck(Now.AddSeconds(1)).Allowed);
        Assert.IsTrue(lifecycle.PassSecurityCheck(Now.AddSeconds(2)).Allowed);
        Assert.IsTrue(lifecycle.PassPolicyCheck(false, Now.AddSeconds(3), TimeSpan.FromMinutes(5)).Allowed);
        var snapshot = CreateSnapshot(data, principal, sessionId);
        Assert.IsTrue(lifecycle.PrepareSubmission(snapshot, principal.PrincipalId, sessionId, Now.AddSeconds(4)).Allowed);
        Assert.IsTrue(lifecycle.MarkUnknown("provider_timeout", Now.AddSeconds(5)).Allowed);

        var resubmit = lifecycle.PrepareSubmission(snapshot, principal.PrincipalId, sessionId, Now.AddSeconds(6));

        Assert.AreEqual(SecurityDenialReason.InvalidStateTransition, resubmit.Reason);
        Assert.AreEqual(WithdrawalLifecycleState.Unknown, lifecycle.State);
        AssertArgumentException(() => lifecycle.MarkCompleted(" ", Now.AddSeconds(7)));
        Assert.IsTrue(lifecycle.MarkCompleted("reconciliation-case-123", Now.AddSeconds(7)).Allowed);
        Assert.AreEqual("reconciliation-case-123", lifecycle.OutcomeEvidenceReference);
    }

    [TestMethod]
    public void TerminalWithdrawalCannotTransitionBackwards()
    {
        var lifecycle = CreateLifecycle();
        Assert.IsTrue(lifecycle.BeginSecurityCheck(Now.AddSeconds(1)).Allowed);
        Assert.IsTrue(lifecycle.PassSecurityCheck(Now.AddSeconds(2)).Allowed);
        Assert.IsTrue(lifecycle.Reject("policy_denied", Now.AddSeconds(3)).Allowed);

        var decision = lifecycle.BeginSecurityCheck(Now.AddSeconds(4));

        Assert.AreEqual(SecurityDenialReason.InvalidStateTransition, decision.Reason);
        Assert.AreEqual(WithdrawalLifecycleState.Rejected, lifecycle.State);
    }

    private static WithdrawalBeneficiaryVersion CreatePendingBeneficiary(Guid customerId) =>
        WithdrawalBeneficiaryVersion.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            customerId,
            DestinationHash,
            Now.AddHours(-1));

    private static WithdrawalBeneficiaryVersion CreateActiveBeneficiary(Guid customerId)
    {
        var beneficiary = CreatePendingBeneficiary(customerId);
        Assert.IsTrue(beneficiary.Verify("verification-1", Now.AddHours(-1), TimeSpan.Zero).Allowed);
        Assert.IsTrue(beneficiary.Activate(Now).Allowed);
        return beneficiary;
    }

    private static WithdrawalTransactionData Transaction(
        Guid withdrawalId,
        Guid customerId,
        Guid beneficiaryVersionId,
        decimal amount) =>
        WithdrawalTransactionData.Create(
            withdrawalId,
            customerId,
            beneficiaryVersionId,
            amount,
            "PKR",
            DestinationHash);

    private static SecurityPrincipal Customer(Guid customerId) =>
        new(
            Guid.NewGuid(),
            PrincipalType.Customer,
            SecurityRole.Investor,
            customerId,
            new HashSet<string>(StringComparer.Ordinal));

    private static SecurityPrincipal Staff(SecurityRole role) =>
        new(
            Guid.NewGuid(),
            PrincipalType.Staff,
            role,
            null,
            new HashSet<string>(StringComparer.Ordinal));

    private static WithdrawalAuthorizationSnapshot CreateSnapshot(
        WithdrawalTransactionData data,
        SecurityPrincipal principal,
        Guid sessionId)
    {
        var stepUp = StepUpGrant.Create(
            principal.PrincipalId,
            sessionId,
            SecurityAction.RequestWithdrawal,
            AuthenticationStrength.PhishingResistant,
            Now.AddMinutes(-1),
            Now.AddMinutes(5));
        var attempt = WithdrawalAuthorizationSnapshot.TryCreate(
            Guid.NewGuid(),
            data,
            principal,
            sessionId,
            stepUp,
            Now,
            TimeSpan.FromMinutes(2));
        return RequiredSnapshot(attempt);
    }

    private static WithdrawalAuthorizationSnapshot RequiredSnapshot(WithdrawalAuthorizationAttempt attempt)
    {
        Assert.IsTrue(attempt.Decision.Allowed);
        return attempt.Snapshot ?? throw new InvalidOperationException("Expected authorization snapshot in test setup.");
    }

    private static WithdrawalRequestLifecycle CreateLifecycle()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(customerId);
        var beneficiary = CreateActiveBeneficiary(customerId);
        var data = Transaction(Guid.NewGuid(), customerId, beneficiary.VersionId, 2500m);
        return RequiredLifecycle(
            WithdrawalRequestLifecycle.TryCreate(data, beneficiary, principal.PrincipalId, Now));
    }

    private static WithdrawalRequestLifecycle RequiredLifecycle(WithdrawalRequestCreation attempt)
    {
        Assert.IsTrue(attempt.Decision.Allowed);
        return attempt.Lifecycle ?? throw new InvalidOperationException("Expected withdrawal lifecycle in test setup.");
    }

    private static void AssertArgumentException(Action action)
    {
        try
        {
            action();
            Assert.Fail("Expected ArgumentException.");
        }
        catch (ArgumentException)
        {
            // Expected.
        }
    }
}
