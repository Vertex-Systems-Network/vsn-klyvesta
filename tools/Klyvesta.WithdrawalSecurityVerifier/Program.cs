using Klyvesta.Application.Identity;
using Klyvesta.Domain.Identity;

var now = new DateTimeOffset(2026, 9, 14, 13, 0, 0, TimeSpan.Zero);
var tests = new (string Id, Action Run)[]
{
    ("WS-001 verified beneficiary withdrawal approved", VerifyWithdrawalApproved),
    ("WS-002 cross-customer beneficiary denied", VerifyCrossCustomerBeneficiary),
    ("WS-003 unverified beneficiary denied", VerifyUnverifiedBeneficiary),
    ("WS-004 beneficiary cool-off enforced", VerifyBeneficiaryCoolingOff),
    ("WS-005 recovery state blocks withdrawal", VerifyRecoveryRestriction),
    ("WS-006 untrusted device blocks withdrawal", VerifyUntrustedDevice),
    ("WS-007 missing step-up blocks withdrawal", VerifyMissingStepUp),
    ("WS-008 invalid amount rejected", VerifyInvalidAmount),
    ("WS-009 server-authoritative session registration", VerifySessionRegistration),
    ("WS-010 conflicting session reference rejected", VerifySessionConflict),
    ("WS-011 single session revocation", VerifySessionRevocation),
    ("WS-012 device revocation revokes matching sessions", VerifyDeviceRevocation),
    ("WS-013 sign-out-all revokes principal sessions", VerifySignOutAll),
    ("WS-014 expired sessions excluded from active inventory", VerifyExpiredSessionFiltering),
    ("WS-015 break-glass maker cannot self-approve", VerifyBreakGlassSelfApprovalDenied),
    ("WS-016 break-glass requires security approver scope", VerifyBreakGlassRoleDenied),
    ("WS-017 break-glass requires phishing-resistant step-up", VerifyBreakGlassWeakStepUpDenied),
    ("WS-018 valid maker-checker break-glass grant is time-bound", VerifyBreakGlassApproved),
    ("WS-019 expired break-glass proposal denied", VerifyExpiredBreakGlassDenied),
};

var failures = new List<string>();
foreach (var (id, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"WITHDRAWAL_SECURITY_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"WITHDRAWAL_SECURITY_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Withdrawal/security assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("PROVIDER_NEUTRAL: no production IdP, bank/beneficiary provider or withdrawal rail is selected.");
Console.WriteLine("NOT_LIVE: no production PII, bank credential, pyPSX credential or real-money movement is exercised.");
return failures.Count == 0 ? 0 : 1;

void VerifyWithdrawalApproved()
{
    var decision = WithdrawalEvaluator().Evaluate(
        CustomerContext("customer-1"),
        Request("customer-1", BeneficiarySecurityState.Verified, now.AddMinutes(-1)),
        now);
    Require(decision.IsAllowed, "verified owned beneficiary with trusted MFA context must pass");
}

void VerifyCrossCustomerBeneficiary()
{
    var decision = WithdrawalEvaluator().Evaluate(
        CustomerContext("customer-1"),
        Request("customer-2", BeneficiarySecurityState.Verified, now.AddMinutes(-1)),
        now);
    Require(!decision.IsAllowed && decision.ReasonCode == "RESOURCE_NOT_FOUND_OR_FORBIDDEN", "cross-customer beneficiary must fail closed");
}

void VerifyUnverifiedBeneficiary()
{
    var decision = WithdrawalEvaluator().Evaluate(
        CustomerContext("customer-1"),
        Request("customer-1", BeneficiarySecurityState.PendingVerification, now.AddMinutes(-1)),
        now);
    Require(!decision.IsAllowed && decision.ReasonCode == "BENEFICIARY_UNVERIFIED", "unverified beneficiary must fail closed");
}

void VerifyBeneficiaryCoolingOff()
{
    var decision = WithdrawalEvaluator().Evaluate(
        CustomerContext("customer-1"),
        Request("customer-1", BeneficiarySecurityState.Verified, now.AddHours(1)),
        now);
    Require(!decision.IsAllowed && decision.ReasonCode == "BENEFICIARY_COOLING_OFF", "cool-off must block withdrawal");
}

void VerifyRecoveryRestriction()
{
    var decision = WithdrawalEvaluator().Evaluate(
        CustomerContext("customer-1", recoveryState: RecoveryState.RecoveredRestricted),
        Request("customer-1", BeneficiarySecurityState.Verified, now.AddMinutes(-1)),
        now);
    Require(!decision.IsAllowed && decision.ReasonCode == "SECURITY_HOLD", "recovered-restricted state must block withdrawal");
}

void VerifyUntrustedDevice()
{
    var decision = WithdrawalEvaluator().Evaluate(
        CustomerContext("customer-1", deviceTrustState: DeviceTrustState.Untrusted),
        Request("customer-1", BeneficiarySecurityState.Verified, now.AddMinutes(-1)),
        now);
    Require(!decision.IsAllowed && decision.ReasonCode == "SECURITY_DEVICE_NOT_TRUSTED", "withdrawal requires trusted device");
}

void VerifyMissingStepUp()
{
    var decision = WithdrawalEvaluator().Evaluate(
        CustomerContext("customer-1", includeStepUp: false),
        Request("customer-1", BeneficiarySecurityState.Verified, now.AddMinutes(-1)),
        now);
    Require(!decision.IsAllowed && decision.ReasonCode == "STEP_UP_REQUIRED", "withdrawal requires action-bound step-up");
}

void VerifyInvalidAmount()
{
    var invalid = Request("customer-1", BeneficiarySecurityState.Verified, now.AddMinutes(-1)) with { Amount = 0m };
    var decision = WithdrawalEvaluator().Evaluate(CustomerContext("customer-1"), invalid, now);
    Require(!decision.IsAllowed && decision.ReasonCode == "WITHDRAWAL_REQUEST_INVALID", "non-positive withdrawal must reject");
}

void VerifySessionRegistration()
{
    var inventory = new SessionDeviceSecurityInventory();
    var session = inventory.Register("session-1", "device-1", CustomerContext("customer-1"), now, now.AddHours(1));
    Require(session.State == SecuritySessionState.Active, "registered session must be active");
    Require(inventory.GetActiveSessions("principal-customer-1", now).Count == 1, "active inventory must contain server-authoritative session");
}

void VerifySessionConflict()
{
    var inventory = new SessionDeviceSecurityInventory();
    _ = inventory.Register("session-2", "device-1", CustomerContext("customer-1"), now, now.AddHours(1));
    var threw = false;
    try
    {
        _ = inventory.Register("session-2", "device-2", CustomerContext("customer-1"), now, now.AddHours(1));
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Require(threw, "same session reference with different evidence must reject");
}

void VerifySessionRevocation()
{
    var inventory = new SessionDeviceSecurityInventory();
    _ = inventory.Register("session-3", "device-1", CustomerContext("customer-1"), now, now.AddHours(1));
    var revoked = inventory.RevokeSession("session-3");
    Require(revoked.State == SecuritySessionState.Revoked, "explicit session revocation must be authoritative");
    Require(inventory.GetActiveSessions("principal-customer-1", now).Count == 0, "revoked session must leave active inventory");
}

void VerifyDeviceRevocation()
{
    var inventory = new SessionDeviceSecurityInventory();
    _ = inventory.Register("session-4", "device-a", CustomerContext("customer-1"), now, now.AddHours(1));
    _ = inventory.Register("session-5", "device-a", CustomerContext("customer-1"), now.AddSeconds(1), now.AddHours(1));
    _ = inventory.Register("session-6", "device-b", CustomerContext("customer-1"), now.AddSeconds(2), now.AddHours(1));

    var count = inventory.RevokeDevice("principal-customer-1", "device-a");
    Require(count == 2, "device revocation must revoke every matching active session");
    Require(inventory.GetActiveSessions("principal-customer-1", now).Single().SessionReference == "session-6", "other device session must remain active");
    Require(inventory.Get("session-4").DeviceTrustState == DeviceTrustState.Revoked, "revoked device session must expose revoked trust state");
}

void VerifySignOutAll()
{
    var inventory = new SessionDeviceSecurityInventory();
    _ = inventory.Register("session-7", "device-a", CustomerContext("customer-1"), now, now.AddHours(1));
    _ = inventory.Register("session-8", "device-b", CustomerContext("customer-1"), now.AddSeconds(1), now.AddHours(1));

    var count = inventory.SignOutAll("principal-customer-1");
    Require(count == 2, "sign-out-all must revoke all active principal sessions");
    Require(inventory.GetActiveSessions("principal-customer-1", now).Count == 0, "no active session may remain");
}

void VerifyExpiredSessionFiltering()
{
    var inventory = new SessionDeviceSecurityInventory();
    _ = inventory.Register("session-9", "device-a", CustomerContext("customer-1"), now.AddHours(-2), now.AddHours(-1));
    Require(inventory.GetActiveSessions("principal-customer-1", now).Count == 0, "expired session must not be considered active");
}

void VerifyBreakGlassSelfApprovalDenied()
{
    var service = new BreakGlassApprovalService();
    var proposal = service.CreateProposal("security-approver", "restore-service", "incident", now, now.AddMinutes(20));
    var threw = TryApprove(service, proposal, SecurityApproverContext("security-approver"), now.AddMinutes(1));
    Require(threw, "maker must not self-approve break-glass");
}

void VerifyBreakGlassRoleDenied()
{
    var service = new BreakGlassApprovalService();
    var proposal = service.CreateProposal("maker-1", "restore-service", "incident", now, now.AddMinutes(20));
    var threw = TryApprove(service, proposal, CustomerContext("customer-1"), now.AddMinutes(1));
    Require(threw, "non-security approver must be denied");
}

void VerifyBreakGlassWeakStepUpDenied()
{
    var service = new BreakGlassApprovalService();
    var proposal = service.CreateProposal("maker-1", "restore-service", "incident", now, now.AddMinutes(20));
    var weak = SecurityApproverContext("approver-1", phishingResistant: false);
    var threw = TryApprove(service, proposal, weak, now.AddMinutes(1));
    Require(threw, "break-glass approval must require phishing-resistant step-up");
}

void VerifyBreakGlassApproved()
{
    var service = new BreakGlassApprovalService();
    var proposal = service.CreateProposal("maker-1", "restore-service", "incident", now, now.AddMinutes(20));
    var grant = service.Approve(proposal, SecurityApproverContext("approver-1"), now.AddMinutes(1));

    Require(grant.ApproverPrincipalId == "approver-1", "independent approver identity must be retained");
    Require(grant.ExpiresAt == now.AddMinutes(16), "grant must be capped to fifteen minutes from approval");
    Require(grant.IsValidFor("maker-1", "restore-service", now.AddMinutes(2)), "approved grant must be action-bound and temporarily valid");
    Require(!grant.IsValidFor("maker-1", "restore-service", now.AddMinutes(17)), "grant must expire");
}

void VerifyExpiredBreakGlassDenied()
{
    var service = new BreakGlassApprovalService();
    var proposal = service.CreateProposal("maker-1", "restore-service", "incident", now.AddMinutes(-30), now.AddMinutes(-1));
    var threw = TryApprove(service, proposal, SecurityApproverContext("approver-1"), now);
    Require(threw, "expired proposal must never approve");
}

WithdrawalSecurityEvaluator WithdrawalEvaluator() => new(new DeterministicIdentityAuthorizationEvaluator());

WithdrawalSecurityRequest Request(string beneficiaryCustomerId, BeneficiarySecurityState state, DateTimeOffset availableAfter) =>
    new(
        "account-1",
        "customer-1",
        new WithdrawalBeneficiaryEvidence("beneficiary-1", beneficiaryCustomerId, state, availableAfter),
        1000m,
        "PKR");

IdentitySecurityContext CustomerContext(
    string customerId,
    RecoveryState recoveryState = RecoveryState.Normal,
    DeviceTrustState deviceTrustState = DeviceTrustState.Trusted,
    bool includeStepUp = true)
{
    var stepUp = includeStepUp
        ? new StepUpGrant(
            IdentityAuthorizationActionNames.StepUpKey(IdentityAuthorizationAction.WithdrawalRequest),
            AuthenticationAssurance.MultiFactor,
            now.AddMinutes(-1),
            now.AddMinutes(5))
        : null;

    return new IdentitySecurityContext(
        IdentityContextAuthority.ServerAuthoritative,
        new IdentityPrincipal($"principal-{customerId}", PrincipalType.Customer, [SecurityRole.Investor], customerId: customerId),
        SecuritySessionState.Active,
        deviceTrustState,
        AccountSecurityState.Normal,
        recoveryState,
        AuthenticationMethod.MultiFactor,
        AuthenticationAssurance.MultiFactor,
        stepUp);
}

IdentitySecurityContext SecurityApproverContext(string principalId, bool phishingResistant = true)
{
    var assurance = phishingResistant ? AuthenticationAssurance.PhishingResistant : AuthenticationAssurance.MultiFactor;
    var method = phishingResistant ? AuthenticationMethod.Passkey : AuthenticationMethod.MultiFactor;
    return new IdentitySecurityContext(
        IdentityContextAuthority.ServerAuthoritative,
        new IdentityPrincipal(
            principalId,
            PrincipalType.Staff,
            [SecurityRole.SecurityAnalyst],
            ["security.breakglass.approve"]),
        SecuritySessionState.Active,
        DeviceTrustState.Trusted,
        AccountSecurityState.Normal,
        RecoveryState.Normal,
        method,
        assurance,
        new StepUpGrant(
            BreakGlassApprovalService.ApprovalStepUpAction,
            assurance,
            now.AddMinutes(-1),
            now.AddMinutes(5)));
}

static bool TryApprove(
    BreakGlassApprovalService service,
    BreakGlassProposal proposal,
    IdentitySecurityContext approver,
    DateTimeOffset approvedAt)
{
    try
    {
        _ = service.Approve(proposal, approver, approvedAt);
        return false;
    }
    catch (InvalidOperationException)
    {
        return true;
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
