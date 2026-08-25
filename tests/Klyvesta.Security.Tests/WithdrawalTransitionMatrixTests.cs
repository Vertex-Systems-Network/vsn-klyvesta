using Klyvesta.Domain.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klyvesta.Security.Tests;

[TestClass]
public sealed class WithdrawalTransitionMatrixTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 8, 25, 21, 0, 0, TimeSpan.Zero);
    private const string DestinationHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static readonly TransitionCommand[] Commands =
    [
        new(
            "BeginSecurityCheck",
            [WithdrawalLifecycleState.Requested],
            WithdrawalLifecycleState.SecurityCheck,
            static (fixture, now) => fixture.Lifecycle.BeginSecurityCheck(now)),
        new(
            "PlaceSecurityHold",
            [WithdrawalLifecycleState.SecurityCheck],
            WithdrawalLifecycleState.SecurityHold,
            static (fixture, now) => fixture.Lifecycle.PlaceSecurityHold("matrix_security_hold", now)),
        new(
            "ResumeSecurityCheck",
            [WithdrawalLifecycleState.SecurityHold],
            WithdrawalLifecycleState.SecurityCheck,
            static (fixture, now) => fixture.Lifecycle.ResumeSecurityCheck(now)),
        new(
            "PassSecurityCheck",
            [WithdrawalLifecycleState.SecurityCheck],
            WithdrawalLifecycleState.PolicyCheck,
            static (fixture, now) => fixture.Lifecycle.PassSecurityCheck(now)),
        new(
            "Reject",
            [WithdrawalLifecycleState.PolicyCheck],
            WithdrawalLifecycleState.Rejected,
            static (fixture, now) => fixture.Lifecycle.Reject("matrix_policy_rejected", now)),
        new(
            "PassPolicyCheckAutoApprove",
            [WithdrawalLifecycleState.PolicyCheck],
            WithdrawalLifecycleState.Approved,
            static (fixture, now) => fixture.Lifecycle.PassPolicyCheck(false, now, TimeSpan.FromMinutes(5))),
        new(
            "PassPolicyCheckProtected",
            [WithdrawalLifecycleState.PolicyCheck],
            WithdrawalLifecycleState.ApprovalPending,
            static (fixture, now) => fixture.Lifecycle.PassPolicyCheck(true, now, TimeSpan.FromMinutes(5))),
        new(
            "TryApprove",
            [WithdrawalLifecycleState.ApprovalPending],
            WithdrawalLifecycleState.Approved,
            static (fixture, now) => fixture.Lifecycle.TryApprove(ComplianceOfficer(), now)),
        new(
            "PrepareSubmission",
            [WithdrawalLifecycleState.Approved],
            WithdrawalLifecycleState.SubmissionPending,
            static (fixture, now) => fixture.Lifecycle.PrepareSubmission(
                CreateSnapshot(fixture, now),
                fixture.Principal.PrincipalId,
                fixture.SessionId,
                now)),
        new(
            "MarkSubmitted",
            [WithdrawalLifecycleState.SubmissionPending],
            WithdrawalLifecycleState.Submitted,
            static (fixture, now) => fixture.Lifecycle.MarkSubmitted("matrix-provider-ref", now)),
        new(
            "MarkProcessing",
            [WithdrawalLifecycleState.Submitted],
            WithdrawalLifecycleState.Processing,
            static (fixture, now) => fixture.Lifecycle.MarkProcessing(now)),
        new(
            "MarkUnknown",
            [
                WithdrawalLifecycleState.SubmissionPending,
                WithdrawalLifecycleState.Submitted,
                WithdrawalLifecycleState.Processing,
            ],
            WithdrawalLifecycleState.Unknown,
            static (fixture, now) => fixture.Lifecycle.MarkUnknown("matrix_provider_ambiguity", now)),
        new(
            "MarkCompleted",
            [WithdrawalLifecycleState.Processing, WithdrawalLifecycleState.Unknown],
            WithdrawalLifecycleState.Completed,
            static (fixture, now) => fixture.Lifecycle.MarkCompleted("matrix-reconciliation-complete", now)),
        new(
            "MarkFailed",
            [
                WithdrawalLifecycleState.Submitted,
                WithdrawalLifecycleState.Processing,
                WithdrawalLifecycleState.Unknown,
            ],
            WithdrawalLifecycleState.Failed,
            static (fixture, now) => fixture.Lifecycle.MarkFailed(
                "matrix_provider_failed",
                "matrix-reconciliation-failed",
                now)),
    ];

    [TestMethod]
    public void EveryTransitionCommandIsAllowedOnlyFromItsCanonicalSourceStates()
    {
        foreach (var sourceState in Enum.GetValues<WithdrawalLifecycleState>())
        {
            foreach (var command in Commands)
            {
                var fixture = CreateAt(sourceState);
                var transitionTime = fixture.Lifecycle.UpdatedAt.AddSeconds(1);
                var decision = command.Invoke(fixture, transitionTime);
                var expectedAllowed = command.AllowedSources.Contains(sourceState);

                if (expectedAllowed)
                {
                    Assert.IsTrue(
                        decision.Allowed,
                        $"{command.Name} should be allowed from {sourceState}.");
                    Assert.AreEqual(
                        command.Destination,
                        fixture.Lifecycle.State,
                        $"{command.Name} should transition {sourceState} to {command.Destination}.");
                    continue;
                }

                Assert.IsFalse(
                    decision.Allowed,
                    $"{command.Name} must be denied from {sourceState}.");
                Assert.AreEqual(
                    SecurityDenialReason.InvalidStateTransition,
                    decision.Reason,
                    $"{command.Name} from {sourceState} must fail closed as an invalid state transition.");
                Assert.AreEqual(
                    sourceState,
                    fixture.Lifecycle.State,
                    $"Denied {command.Name} must not mutate lifecycle state {sourceState}.");
            }
        }
    }

    private static TransitionFixture CreateAt(WithdrawalLifecycleState targetState)
    {
        var fixture = CreateRequested();

        if (targetState is WithdrawalLifecycleState.Requested)
        {
            return fixture;
        }

        Allow(fixture.Lifecycle.BeginSecurityCheck(BaseTime.AddSeconds(1)), "build SecurityCheck fixture");
        if (targetState is WithdrawalLifecycleState.SecurityCheck)
        {
            return fixture;
        }

        if (targetState is WithdrawalLifecycleState.SecurityHold)
        {
            Allow(
                fixture.Lifecycle.PlaceSecurityHold("matrix_fixture_hold", BaseTime.AddSeconds(2)),
                "build SecurityHold fixture");
            return fixture;
        }

        Allow(fixture.Lifecycle.PassSecurityCheck(BaseTime.AddSeconds(2)), "build PolicyCheck fixture");
        if (targetState is WithdrawalLifecycleState.PolicyCheck)
        {
            return fixture;
        }

        if (targetState is WithdrawalLifecycleState.Rejected)
        {
            Allow(
                fixture.Lifecycle.Reject("matrix_fixture_rejected", BaseTime.AddSeconds(3)),
                "build Rejected fixture");
            return fixture;
        }

        if (targetState is WithdrawalLifecycleState.ApprovalPending)
        {
            Allow(
                fixture.Lifecycle.PassPolicyCheck(true, BaseTime.AddSeconds(3), TimeSpan.FromMinutes(5)),
                "build ApprovalPending fixture");
            return fixture;
        }

        Allow(
            fixture.Lifecycle.PassPolicyCheck(false, BaseTime.AddSeconds(3), TimeSpan.FromMinutes(5)),
            "build Approved fixture");
        if (targetState is WithdrawalLifecycleState.Approved)
        {
            return fixture;
        }

        var prepareTime = BaseTime.AddSeconds(4);
        Allow(
            fixture.Lifecycle.PrepareSubmission(
                CreateSnapshot(fixture, prepareTime),
                fixture.Principal.PrincipalId,
                fixture.SessionId,
                prepareTime),
            "build SubmissionPending fixture");
        if (targetState is WithdrawalLifecycleState.SubmissionPending)
        {
            return fixture;
        }

        if (targetState is WithdrawalLifecycleState.Unknown)
        {
            Allow(
                fixture.Lifecycle.MarkUnknown("matrix_fixture_unknown", BaseTime.AddSeconds(5)),
                "build Unknown fixture");
            return fixture;
        }

        Allow(
            fixture.Lifecycle.MarkSubmitted("matrix-fixture-provider-ref", BaseTime.AddSeconds(5)),
            "build Submitted fixture");
        if (targetState is WithdrawalLifecycleState.Submitted)
        {
            return fixture;
        }

        if (targetState is WithdrawalLifecycleState.Failed)
        {
            Allow(
                fixture.Lifecycle.MarkFailed(
                    "matrix_fixture_failed",
                    "matrix-fixture-failed-evidence",
                    BaseTime.AddSeconds(6)),
                "build Failed fixture");
            return fixture;
        }

        Allow(fixture.Lifecycle.MarkProcessing(BaseTime.AddSeconds(6)), "build Processing fixture");
        if (targetState is WithdrawalLifecycleState.Processing)
        {
            return fixture;
        }

        if (targetState is WithdrawalLifecycleState.Completed)
        {
            Allow(
                fixture.Lifecycle.MarkCompleted(
                    "matrix-fixture-completed-evidence",
                    BaseTime.AddSeconds(7)),
                "build Completed fixture");
            return fixture;
        }

        throw new InvalidOperationException($"No canonical fixture path is defined for {targetState}.");
    }

    private static TransitionFixture CreateRequested()
    {
        var customerId = Guid.NewGuid();
        var principal = Customer(customerId);
        var beneficiary = WithdrawalBeneficiaryVersion.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            customerId,
            DestinationHash,
            BaseTime.AddHours(-2));
        Allow(
            beneficiary.Verify("matrix-beneficiary-verification", BaseTime.AddHours(-2), TimeSpan.Zero),
            "verify matrix beneficiary");
        Allow(beneficiary.Activate(BaseTime.AddHours(-1)), "activate matrix beneficiary");

        var transaction = WithdrawalTransactionData.Create(
            Guid.NewGuid(),
            customerId,
            beneficiary.VersionId,
            2500m,
            "PKR",
            DestinationHash);
        var creation = WithdrawalRequestLifecycle.TryCreate(
            transaction,
            beneficiary,
            principal.PrincipalId,
            BaseTime);

        Assert.IsTrue(creation.Decision.Allowed, "Matrix fixture withdrawal creation must succeed.");
        var lifecycle = creation.Lifecycle ??
            throw new InvalidOperationException("Expected matrix fixture withdrawal lifecycle.");
        return new TransitionFixture(lifecycle, principal, Guid.NewGuid());
    }

    private static WithdrawalAuthorizationSnapshot CreateSnapshot(
        TransitionFixture fixture,
        DateTimeOffset authorizedAt)
    {
        var stepUp = StepUpGrant.Create(
            fixture.Principal.PrincipalId,
            fixture.SessionId,
            SecurityAction.RequestWithdrawal,
            AuthenticationStrength.PhishingResistant,
            authorizedAt.AddMinutes(-1),
            authorizedAt.AddMinutes(5));
        var attempt = WithdrawalAuthorizationSnapshot.TryCreate(
            Guid.NewGuid(),
            fixture.Lifecycle.TransactionData,
            fixture.Principal,
            fixture.SessionId,
            stepUp,
            authorizedAt,
            TimeSpan.FromMinutes(2));

        Assert.IsTrue(attempt.Decision.Allowed, "Matrix fixture authorization snapshot must succeed.");
        return attempt.Snapshot ??
            throw new InvalidOperationException("Expected matrix fixture authorization snapshot.");
    }

    private static SecurityPrincipal Customer(Guid customerId) =>
        new(
            Guid.NewGuid(),
            PrincipalType.Customer,
            SecurityRole.Investor,
            customerId,
            new HashSet<string>(StringComparer.Ordinal));

    private static SecurityPrincipal ComplianceOfficer() =>
        new(
            Guid.NewGuid(),
            PrincipalType.Staff,
            SecurityRole.ComplianceOfficer,
            null,
            new HashSet<string>(StringComparer.Ordinal));

    private static void Allow(SecurityDecision decision, string operation)
    {
        Assert.IsTrue(decision.Allowed, $"Expected canonical fixture operation to succeed: {operation}.");
    }

    private sealed record TransitionFixture(
        WithdrawalRequestLifecycle Lifecycle,
        SecurityPrincipal Principal,
        Guid SessionId);

    private sealed record TransitionCommand(
        string Name,
        IReadOnlySet<WithdrawalLifecycleState> AllowedSources,
        WithdrawalLifecycleState Destination,
        Func<TransitionFixture, DateTimeOffset, SecurityDecision> Invoke)
    {
        public TransitionCommand(
            string name,
            WithdrawalLifecycleState[] allowedSources,
            WithdrawalLifecycleState destination,
            Func<TransitionFixture, DateTimeOffset, SecurityDecision> invoke)
            : this(name, allowedSources.ToHashSet(), destination, invoke)
        {
        }
    }
}
