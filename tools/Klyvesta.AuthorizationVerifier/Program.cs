using Klyvesta.Application.Identity;
using Klyvesta.Domain.Identity;

var tests = new (string Id, Func<Task> Run)[]
{
    ("AUTH-001 blank session reference fails closed", VerifyBlankSessionFailsClosedAsync),
    ("AUTH-002 provider-neutral resolver creates server-authoritative context", VerifyProviderNeutralResolverAsync),
    ("AUTH-003 customer evidence without customer ownership identity fails closed", VerifyCustomerWithoutCustomerIdFailsClosedAsync),
    ("AUTH-004 unknown role evidence fails closed", VerifyUnknownRoleFailsClosedAsync),
    ("AUTHZ-001 unauthenticated context denies", () => VerifyDeniedAsync(IdentitySecurityContext.FailClosedUnauthenticated, Request(IdentityAuthorizationAction.PortfolioRead, "portfolio-1", "customer-1"), "AUTH_REQUIRED")),
    ("AUTHZ-002 unknown action denies", () => VerifyDeniedAsync(CustomerContext(), Request(IdentityAuthorizationAction.Unknown, "resource-1", "customer-1"), "IDENTITY_AUTHZ_INVALID_REQUEST")),
    ("AUTHZ-003 investor can read own portfolio", VerifyInvestorOwnPortfolioAsync),
    ("AUTHZ-004 investor cannot read another customer portfolio", () => VerifyDeniedAsync(CustomerContext(), Request(IdentityAuthorizationAction.PortfolioRead, "portfolio-2", "customer-2"), "RESOURCE_NOT_FOUND_OR_FORBIDDEN")),
    ("AUTHZ-005 staff plus investor role-shape spoofing denies", VerifyStaffInvestorSpoofDeniedAsync),
    ("AUTHZ-006 customer plus execution-validator role-shape spoofing denies", VerifyCustomerExecutionValidatorSpoofDeniedAsync),
    ("AUTHZ-007 support cannot create customer financial intent", () => VerifyDeniedAsync(StaffContext(SecurityRole.SupportL1), Request(IdentityAuthorizationAction.ManualOrderCreate, "account-1", "customer-1"), "IDENTITY_AUTHZ_DENY")),
    ("AUTHZ-008 restricted account denies manual order identity authorization", () => VerifyDeniedAsync(CustomerContext(accountState: AccountSecurityState.Restricted), Request(IdentityAuthorizationAction.ManualOrderCreate, "account-1", "customer-1"), "ACCOUNT_RESTRICTED")),
    ("AUTHZ-009 recovery hold denies withdrawal identity authorization", () => VerifyDeniedAsync(CustomerContext(recoveryState: RecoveryState.SecurityHold), Request(IdentityAuthorizationAction.WithdrawalRequest, "withdrawal-1", "customer-1"), "SECURITY_HOLD")),
    ("AUTHZ-010 revoked device denies withdrawal", () => VerifyDeniedAsync(CustomerContext(deviceState: DeviceTrustState.Revoked), Request(IdentityAuthorizationAction.WithdrawalRequest, "withdrawal-1", "customer-1"), "SECURITY_DEVICE_REVOKED")),
    ("AUTHZ-011 withdrawal requires strong action-bound step-up", () => VerifyDeniedAsync(CustomerContext(), Request(IdentityAuthorizationAction.WithdrawalRequest, "withdrawal-1", "customer-1"), "STEP_UP_REQUIRED")),
    ("AUTHZ-012 valid withdrawal step-up allows identity layer", VerifyValidWithdrawalStepUpAsync),
    ("AUTHZ-013 wrong-action step-up cannot authorize withdrawal", VerifyWrongActionStepUpDeniedAsync),
    ("AUTHZ-014 security analyst revoke requires explicit scope", VerifySecurityAnalystScopeDeniedAsync),
    ("AUTHZ-015 security analyst scoped revoke still requires step-up", VerifySecurityAnalystStepUpDeniedAsync),
    ("AUTHZ-016 scoped security analyst with step-up may revoke", VerifySecurityAnalystRevokeAllowedAsync),
    ("AUTHZ-017 risk maker cannot approve own class of protected action", () => VerifyDeniedAsync(StaffContext(SecurityRole.RiskAnalyst, scopes: ["risk.policy.propose", "risk.policy.approve"]), Request(IdentityAuthorizationAction.RiskPolicyApprove, "risk-policy-1"), "IDENTITY_AUTHZ_DENY")),
    ("AUTHZ-018 risk approver cannot become maker by scope injection", () => VerifyDeniedAsync(StaffContext(SecurityRole.RiskApprover, scopes: ["risk.policy.propose", "risk.policy.approve"]), Request(IdentityAuthorizationAction.RiskPolicyPropose, "risk-policy-1"), "IDENTITY_AUTHZ_DENY")),
    ("AUTHZ-019 AI rebalance agent may create scoped proposal only", VerifyRebalanceProposalAllowedAsync),
    ("AUTHZ-020 AI rebalance agent cannot submit broker order", () => VerifyDeniedAsync(AiContext(SecurityRole.RebalanceAgent, ["rebalance.proposal.create", "broker.authorized-order.submit"]), Request(IdentityAuthorizationAction.AuthorizedBrokerOrderSubmit, "broker-order-1"), "IDENTITY_AUTHZ_DENY")),
    ("AUTHZ-021 machine role without declared scope denies", () => VerifyDeniedAsync(ServiceContext(SecurityRole.ExecutionValidator), Request(IdentityAuthorizationAction.AuthorizedBrokerOrderSubmit, "broker-order-1"), "IDENTITY_SCOPE_REQUIRED")),
    ("AUTHZ-022 execution validator with scope passes identity boundary", VerifyExecutionValidatorAllowedAsync),
    ("AUTHZ-023 broker adapter cannot impersonate execution validator", () => VerifyDeniedAsync(ServiceContext(SecurityRole.BrokerAdapter, ["broker.authorized-order.submit", "broker.operation.execute"]), Request(IdentityAuthorizationAction.AuthorizedBrokerOrderSubmit, "broker-order-1"), "IDENTITY_AUTHZ_DENY")),
    ("AUTHZ-024 notification service requires sanitized-send scope", VerifyNotificationServiceAllowedAsync),
};

var failures = new List<string>();
foreach (var (id, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"AUTHORIZATION_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"AUTHORIZATION_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Authorization assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("SERVER_AUTHORITY: authentication input is an opaque session reference; principal/roles/scopes/customer ownership come only from the trusted evidence-source abstraction.");
Console.WriteLine("DENY_BY_DEFAULT: unknown actions, incompatible principal-role shapes, missing scopes, cross-customer resources, restricted security state and missing step-up evidence deny deterministically.");
Console.WriteLine("PRIVILEGE_BOUNDARY: customer, staff, service and AI-agent roles are disjoint; scopes cannot convert a role into another authority class.");
Console.WriteLine("IDENTITY_ONLY: an ALLOW result is identity-layer authorization only; risk, compliance, entitlement, legal/product, broker and business-invariant gates remain independently mandatory.");
Console.WriteLine("NOT_LIVE: no IdP/provider selection, bearer-token parsing, production PII, broker credentials, live pyPSX integration or real-money authority is implemented or exercised.");

return failures.Count == 0 ? 0 : 1;

static async Task VerifyBlankSessionFailsClosedAsync()
{
    var source = new FixedEvidenceSource(ValidEvidence());
    var provider = new ProviderNeutralAuthenticationContextProvider(source);
    var context = await provider.ResolveAsync(new AuthenticationContextRequest(" "));
    Require(!context.IsAuthenticated, "blank opaque session reference must fail closed");
    Require(source.ResolveCount == 0, "blank request must not call evidence source");
}

static async Task VerifyProviderNeutralResolverAsync()
{
    var source = new FixedEvidenceSource(ValidEvidence());
    var provider = new ProviderNeutralAuthenticationContextProvider(source);
    var context = await provider.ResolveAsync(new AuthenticationContextRequest("session-ref-1"));
    Require(context.IsAuthenticated, "valid trusted evidence must produce authenticated context");
    Require(context.Authority == IdentityContextAuthority.ServerAuthoritative, "resolver must stamp server authority internally");
    Require(context.Principal?.PrincipalId == "principal-1", "principal must come from trusted evidence");
    Require(context.Principal?.CustomerId == "customer-1", "customer ownership identity must come from trusted evidence");
    Require(source.ResolveCount == 1, "evidence source must be queried exactly once");
}

static async Task VerifyCustomerWithoutCustomerIdFailsClosedAsync()
{
    var source = new FixedEvidenceSource(ValidEvidence(customerId: null));
    var provider = new ProviderNeutralAuthenticationContextProvider(source);
    var context = await provider.ResolveAsync(new AuthenticationContextRequest("session-ref-1"));
    Require(!context.IsAuthenticated, "customer principal without customer ownership identity must fail closed");
}

static async Task VerifyUnknownRoleFailsClosedAsync()
{
    var source = new FixedEvidenceSource(ValidEvidence(roles: [SecurityRole.Unknown]));
    var provider = new ProviderNeutralAuthenticationContextProvider(source);
    var context = await provider.ResolveAsync(new AuthenticationContextRequest("session-ref-1"));
    Require(!context.IsAuthenticated, "unknown role evidence must fail closed before authorization");
}

static Task VerifyInvestorOwnPortfolioAsync()
{
    var decision = Evaluate(CustomerContext(), Request(IdentityAuthorizationAction.PortfolioRead, "portfolio-1", "customer-1"));
    Require(decision.IsIdentityAuthorized, "investor must pass identity authorization for own portfolio");
    Require(decision.ReasonCode == "IDENTITY_AUTHZ_ALLOW", "allow reason must be explicit");
    Require(decision.PolicyVersion == DeterministicIdentityAuthorizationEvaluator.CurrentPolicyVersion, "decision must retain policy version");
    return Task.CompletedTask;
}

static Task VerifyStaffInvestorSpoofDeniedAsync()
{
    var principal = new IdentityPrincipal("staff-1", PrincipalType.Staff, [SecurityRole.SupportL1, SecurityRole.Investor]);
    var context = Context(principal);
    return VerifyDeniedAsync(context, Request(IdentityAuthorizationAction.PortfolioRead, "portfolio-1", "customer-1"), "IDENTITY_PRINCIPAL_ROLE_INVALID");
}

static Task VerifyCustomerExecutionValidatorSpoofDeniedAsync()
{
    var principal = new IdentityPrincipal("principal-1", PrincipalType.Customer, [SecurityRole.Investor, SecurityRole.ExecutionValidator], customerId: "customer-1");
    var context = Context(principal);
    return VerifyDeniedAsync(context, Request(IdentityAuthorizationAction.AuthorizedBrokerOrderSubmit, "broker-order-1"), "IDENTITY_PRINCIPAL_ROLE_INVALID");
}

static Task VerifyValidWithdrawalStepUpAsync()
{
    var now = Now();
    var stepUp = new StepUpGrant(
        IdentityAuthorizationActionNames.StepUpKey(IdentityAuthorizationAction.WithdrawalRequest),
        AuthenticationAssurance.MultiFactor,
        now.AddMinutes(-2),
        now.AddMinutes(10));
    var decision = Evaluate(CustomerContext(assurance: AuthenticationAssurance.MultiFactor, stepUp: stepUp), Request(IdentityAuthorizationAction.WithdrawalRequest, "withdrawal-1", "customer-1"), now);
    Require(decision.IsIdentityAuthorized, "valid action-bound withdrawal step-up must pass identity layer");
    return Task.CompletedTask;
}

static Task VerifyWrongActionStepUpDeniedAsync()
{
    var now = Now();
    var stepUp = new StepUpGrant(
        IdentityAuthorizationActionNames.StepUpKey(IdentityAuthorizationAction.BeneficiaryManage),
        AuthenticationAssurance.MultiFactor,
        now.AddMinutes(-2),
        now.AddMinutes(10));
    return VerifyDeniedAsync(
        CustomerContext(assurance: AuthenticationAssurance.MultiFactor, stepUp: stepUp),
        Request(IdentityAuthorizationAction.WithdrawalRequest, "withdrawal-1", "customer-1"),
        "STEP_UP_REQUIRED",
        now);
}

static Task VerifySecurityAnalystScopeDeniedAsync()
{
    var now = Now();
    var stepUp = new StepUpGrant(
        IdentityAuthorizationActionNames.StepUpKey(IdentityAuthorizationAction.SessionDeviceRevoke),
        AuthenticationAssurance.MultiFactor,
        now.AddMinutes(-1),
        now.AddMinutes(10));
    return VerifyDeniedAsync(
        StaffContext(SecurityRole.SecurityAnalyst, assurance: AuthenticationAssurance.MultiFactor, stepUp: stepUp),
        Request(IdentityAuthorizationAction.SessionDeviceRevoke, "session-1"),
        "IDENTITY_SCOPE_REQUIRED",
        now);
}

static Task VerifySecurityAnalystStepUpDeniedAsync() =>
    VerifyDeniedAsync(
        StaffContext(SecurityRole.SecurityAnalyst, scopes: ["security.sessions.revoke"]),
        Request(IdentityAuthorizationAction.SessionDeviceRevoke, "session-1"),
        "STEP_UP_REQUIRED");

static Task VerifySecurityAnalystRevokeAllowedAsync()
{
    var now = Now();
    var stepUp = new StepUpGrant(
        IdentityAuthorizationActionNames.StepUpKey(IdentityAuthorizationAction.SessionDeviceRevoke),
        AuthenticationAssurance.MultiFactor,
        now.AddMinutes(-1),
        now.AddMinutes(10));
    var decision = Evaluate(
        StaffContext(SecurityRole.SecurityAnalyst, ["security.sessions.revoke"], AuthenticationAssurance.MultiFactor, stepUp: stepUp),
        Request(IdentityAuthorizationAction.SessionDeviceRevoke, "session-1"),
        now);
    Require(decision.IsIdentityAuthorized, "scoped security analyst with action-bound step-up must be allowed at identity layer");
    return Task.CompletedTask;
}

static Task VerifyRebalanceProposalAllowedAsync()
{
    var decision = Evaluate(
        AiContext(SecurityRole.RebalanceAgent, ["rebalance.proposal.create"]),
        Request(IdentityAuthorizationAction.RebalanceProposalCreate, "proposal-1"));
    Require(decision.IsIdentityAuthorized, "rebalance agent with exact proposal scope must pass identity layer");
    return Task.CompletedTask;
}

static Task VerifyExecutionValidatorAllowedAsync()
{
    var decision = Evaluate(
        ServiceContext(SecurityRole.ExecutionValidator, ["broker.authorized-order.submit"]),
        Request(IdentityAuthorizationAction.AuthorizedBrokerOrderSubmit, "broker-order-1"));
    Require(decision.IsIdentityAuthorized, "execution validator with narrow scope must pass identity layer");
    return Task.CompletedTask;
}

static Task VerifyNotificationServiceAllowedAsync()
{
    var decision = Evaluate(
        ServiceContext(SecurityRole.NotificationService, ["notifications.sanitized.send"]),
        Request(IdentityAuthorizationAction.SanitizedNotificationSend, "notification-1"));
    Require(decision.IsIdentityAuthorized, "notification service with sanitized-send scope must pass identity layer");
    return Task.CompletedTask;
}

static Task VerifyDeniedAsync(
    IdentitySecurityContext context,
    IdentityAuthorizationRequest request,
    string reasonCode,
    DateTimeOffset? evaluatedAt = null)
{
    var decision = Evaluate(context, request, evaluatedAt ?? Now());
    Require(decision.Outcome == IdentityAuthorizationOutcome.Deny, $"expected DENY for {reasonCode}, got {decision.Outcome}");
    Require(decision.ReasonCode == reasonCode, $"expected denial reason {reasonCode}, got {decision.ReasonCode}");
    Require(decision.PolicyVersion == DeterministicIdentityAuthorizationEvaluator.CurrentPolicyVersion, "denial must retain policy version");
    return Task.CompletedTask;
}

static IdentityAuthorizationDecision Evaluate(
    IdentitySecurityContext context,
    IdentityAuthorizationRequest request,
    DateTimeOffset? evaluatedAt = null) =>
    new DeterministicIdentityAuthorizationEvaluator().Evaluate(context, request, evaluatedAt ?? Now());

static IdentityAuthorizationRequest Request(
    IdentityAuthorizationAction action,
    string resourceReference,
    string? ownerCustomerId = null) =>
    new(action, new ResolvedAuthorizationResource(resourceReference, ownerCustomerId));

static IdentitySecurityContext CustomerContext(
    AccountSecurityState accountState = AccountSecurityState.Normal,
    RecoveryState recoveryState = RecoveryState.Normal,
    DeviceTrustState deviceState = DeviceTrustState.Trusted,
    AuthenticationAssurance assurance = AuthenticationAssurance.PhishingResistant,
    StepUpGrant? stepUp = null) =>
    Context(
        new IdentityPrincipal("principal-1", PrincipalType.Customer, [SecurityRole.Investor], customerId: "customer-1"),
        accountState,
        recoveryState,
        deviceState,
        assurance,
        stepUp);

static IdentitySecurityContext StaffContext(
    SecurityRole role,
    IReadOnlyCollection<string>? scopes = null,
    AuthenticationAssurance assurance = AuthenticationAssurance.PhishingResistant,
    DeviceTrustState deviceState = DeviceTrustState.Trusted,
    StepUpGrant? stepUp = null) =>
    Context(
        new IdentityPrincipal("staff-1", PrincipalType.Staff, [role], scopes ?? []),
        deviceState: deviceState,
        assurance: assurance,
        stepUp: stepUp);

static IdentitySecurityContext ServiceContext(
    SecurityRole role,
    IReadOnlyCollection<string>? scopes = null) =>
    Context(
        new IdentityPrincipal("service-1", PrincipalType.Service, [role], scopes ?? []),
        deviceState: DeviceTrustState.Unknown,
        authenticationMethod: AuthenticationMethod.WorkloadIdentity,
        assurance: AuthenticationAssurance.PhishingResistant);

static IdentitySecurityContext AiContext(
    SecurityRole role,
    IReadOnlyCollection<string>? scopes = null) =>
    Context(
        new IdentityPrincipal("ai-1", PrincipalType.AiAgent, [role], scopes ?? []),
        deviceState: DeviceTrustState.Unknown,
        authenticationMethod: AuthenticationMethod.WorkloadIdentity,
        assurance: AuthenticationAssurance.PhishingResistant);

static IdentitySecurityContext Context(
    IdentityPrincipal principal,
    AccountSecurityState accountState = AccountSecurityState.Normal,
    RecoveryState recoveryState = RecoveryState.Normal,
    DeviceTrustState deviceState = DeviceTrustState.Trusted,
    AuthenticationAssurance assurance = AuthenticationAssurance.PhishingResistant,
    StepUpGrant? stepUp = null,
    AuthenticationMethod authenticationMethod = AuthenticationMethod.Passkey) =>
    new(
        IdentityContextAuthority.ServerAuthoritative,
        principal,
        SecuritySessionState.Active,
        deviceState,
        accountState,
        recoveryState,
        authenticationMethod,
        assurance,
        stepUp);

static TrustedIdentityEvidence ValidEvidence(
    string? customerId = "customer-1",
    IReadOnlyCollection<SecurityRole>? roles = null) =>
    new(
        "principal-1",
        PrincipalType.Customer,
        roles ?? [SecurityRole.Investor],
        [],
        customerId,
        SecuritySessionState.Active,
        DeviceTrustState.Trusted,
        AccountSecurityState.Normal,
        RecoveryState.Normal,
        AuthenticationMethod.Passkey,
        AuthenticationAssurance.PhishingResistant,
        StepUpGrant: null);

static DateTimeOffset Now() => new(2026, 9, 3, 0, 20, 0, TimeSpan.Zero);

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

file sealed class FixedEvidenceSource : IIdentityEvidenceSource
{
    private readonly TrustedIdentityEvidence? _evidence;

    public FixedEvidenceSource(TrustedIdentityEvidence? evidence)
    {
        _evidence = evidence;
    }

    public int ResolveCount { get; private set; }

    public ValueTask<TrustedIdentityEvidence?> ResolveAsync(
        string sessionReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ResolveCount++;
        return ValueTask.FromResult(_evidence);
    }
}
