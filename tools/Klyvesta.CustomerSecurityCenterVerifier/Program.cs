using Klyvesta.Application.SecurityCenter;
using Klyvesta.Domain.Identity;
using Klyvesta.Domain.SecurityCenter;

var failures = new List<string>();
var passes = 0;
var projector = new DeterministicCustomerSecurityCenterProjector();
var customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var otherCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
var observedAt = new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);

Check("SEC-001", "same inputs produce deterministic security-center output", () =>
{
    var first = projector.Project(CreateRequest());
    var second = projector.Project(CreateRequest());
    Require(first == second, "security-center projection must be deterministic");
});

Check("SEC-002", "cross-customer request fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(authenticatedCustomerId: otherCustomerId)),
        "CUSTOMER_SECURITY_CENTER_SCOPE_MISMATCH");
});

Check("SEC-003", "missing authenticated customer id is rejected", () =>
{
    RequireThrows<ArgumentException>(() =>
        projector.Project(CreateRequest(authenticatedCustomerId: Guid.Empty)));
});

Check("SEC-004", "missing requested customer id is rejected", () =>
{
    RequireThrows<ArgumentException>(() =>
        projector.Project(CreateRequest(requestCustomerId: Guid.Empty)));
});

Check("SEC-005", "blank session reference is rejected", () =>
{
    RequireThrows<ArgumentException>(() =>
        projector.Project(CreateRequest(sessionReference: " ")));
});

Check("SEC-006", "blank device reference is rejected", () =>
{
    RequireThrows<ArgumentException>(() =>
        projector.Project(CreateRequest(deviceReference: " ")));
});

Check("SEC-007", "missing observation time is rejected", () =>
{
    RequireThrows<ArgumentException>(() =>
        projector.Project(CreateRequest(requestObservedAt: default)));
});

Check("SEC-008", "non-authoritative identity context fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(authority: IdentityContextAuthority.Unknown))),
        "CUSTOMER_SECURITY_CENTER_IDENTITY_NOT_SERVER_AUTHORITATIVE");
});

Check("SEC-009", "missing principal fails closed", () =>
{
    var context = new IdentitySecurityContext(
        IdentityContextAuthority.ServerAuthoritative,
        Principal: null,
        SecuritySessionState.Active,
        DeviceTrustState.Trusted,
        AccountSecurityState.Normal,
        RecoveryState.Normal,
        AuthenticationMethod.Passkey,
        AuthenticationAssurance.PhishingResistant,
        StepUpGrant: null);

    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: context)),
        "CUSTOMER_SECURITY_CENTER_PRINCIPAL_REQUIRED");
});

Check("SEC-010", "staff principal cannot enter customer security center", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(principalType: PrincipalType.Staff))),
        "CUSTOMER_SECURITY_CENTER_CUSTOMER_PRINCIPAL_REQUIRED");
});

Check("SEC-011", "principal customer id must be structurally valid", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(principalCustomerId: "not-a-guid"))),
        "CUSTOMER_SECURITY_CENTER_PRINCIPAL_SCOPE_MISMATCH");
});

Check("SEC-012", "principal customer scope mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(principalCustomerId: otherCustomerId.ToString("D")))),
        "CUSTOMER_SECURITY_CENTER_PRINCIPAL_SCOPE_MISMATCH");
});

Check("SEC-013", "customer principal must carry investor security role", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(roles: new[] { SecurityRole.SupportL1 }))),
        "CUSTOMER_SECURITY_CENTER_INVESTOR_ROLE_REQUIRED");
});

Check("SEC-014", "unknown session state is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(sessionState: SecuritySessionState.Unknown))),
        "CUSTOMER_SECURITY_CENTER_IDENTITY_CONTEXT_INCOMPLETE");
});

Check("SEC-015", "revoked session cannot authorize projection", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(sessionState: SecuritySessionState.Revoked))),
        "CUSTOMER_SECURITY_CENTER_ACTIVE_AUTHENTICATION_REQUIRED");
});

Check("SEC-016", "expired session cannot authorize projection", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(sessionState: SecuritySessionState.Expired))),
        "CUSTOMER_SECURITY_CENTER_ACTIVE_AUTHENTICATION_REQUIRED");
});

Check("SEC-017", "unknown device trust state is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(deviceState: DeviceTrustState.Unknown))),
        "CUSTOMER_SECURITY_CENTER_IDENTITY_CONTEXT_INCOMPLETE");
});

Check("SEC-018", "unknown account security state is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(accountState: AccountSecurityState.Unknown))),
        "CUSTOMER_SECURITY_CENTER_IDENTITY_CONTEXT_INCOMPLETE");
});

Check("SEC-019", "unknown recovery state is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(recoveryState: RecoveryState.Unknown))),
        "CUSTOMER_SECURITY_CENTER_IDENTITY_CONTEXT_INCOMPLETE");
});

Check("SEC-020", "unknown authentication method is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(authenticationMethod: AuthenticationMethod.Unknown))),
        "CUSTOMER_SECURITY_CENTER_IDENTITY_CONTEXT_INCOMPLETE");
});

Check("SEC-021", "workload identity is invalid for customer projection", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(authenticationMethod: AuthenticationMethod.WorkloadIdentity))),
        "CUSTOMER_SECURITY_CENTER_CUSTOMER_AUTH_METHOD_INVALID");
});

Check("SEC-022", "unknown authentication assurance is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => projector.Project(CreateRequest(identityContext: CreateContext(authenticationAssurance: AuthenticationAssurance.Unknown))),
        "CUSTOMER_SECURITY_CENTER_IDENTITY_CONTEXT_INCOMPLETE");
});

Check("SEC-023", "trusted passkey phishing-resistant context is clear", () =>
{
    var result = projector.Project(CreateRequest());
    Require(result.Posture == CustomerSecurityCenterPosture.Clear, "trusted passkey context should be clear");
    Require(result.Signals.Count == 0, "clear context should emit no warning signal");
    Require(result.IsAuthenticated, "result must propagate authenticated identity state");
});

Check("SEC-024", "untrusted device raises elevated posture", () =>
{
    var result = projector.Project(CreateRequest(identityContext: CreateContext(deviceState: DeviceTrustState.Untrusted)));
    Require(result.Posture == CustomerSecurityCenterPosture.Elevated, "untrusted device should be elevated");
    Require(result.Signals.Contains("DEVICE_UNTRUSTED"), "untrusted device signal required");
});

Check("SEC-025", "restricted device raises restricted posture", () =>
{
    var result = projector.Project(CreateRequest(identityContext: CreateContext(deviceState: DeviceTrustState.Restricted)));
    Require(result.Posture == CustomerSecurityCenterPosture.Restricted, "restricted device should be restricted posture");
    Require(result.Signals.Contains("DEVICE_RESTRICTED"), "restricted device signal required");
});

Check("SEC-026", "recovery security hold propagates effective restriction", () =>
{
    var result = projector.Project(CreateRequest(identityContext: CreateContext(recoveryState: RecoveryState.SecurityHold)));
    Require(result.Posture == CustomerSecurityCenterPosture.Restricted, "security hold should restrict posture");
    Require(result.EffectiveAccountSecurityState == AccountSecurityState.Restricted, "recovery hold must propagate effective restriction");
    Require(result.Signals.Contains("RECOVERY_SECURITYHOLD"), "recovery hold signal required");
    Require(result.Signals.Contains("ACCOUNT_RESTRICTED"), "effective restricted account signal required");
});

Check("SEC-027", "suspended account remains restricted", () =>
{
    var result = projector.Project(CreateRequest(identityContext: CreateContext(accountState: AccountSecurityState.Suspended)));
    Require(result.Posture == CustomerSecurityCenterPosture.Restricted, "suspended account should be restricted");
    Require(result.Signals.Contains("ACCOUNT_SUSPENDED"), "suspended account signal required");
});

Check("SEC-028", "single-factor password context is elevated", () =>
{
    var result = projector.Project(CreateRequest(identityContext: CreateContext(
        authenticationMethod: AuthenticationMethod.Password,
        authenticationAssurance: AuthenticationAssurance.SingleFactor)));
    Require(result.Posture == CustomerSecurityCenterPosture.Elevated, "single-factor password should be elevated");
    Require(result.Signals.Contains("AUTH_PASSWORD"), "password signal required");
    Require(result.Signals.Contains("AUTH_SINGLE_FACTOR"), "single-factor signal required");
});

Check("SEC-029", "multi-factor context is review posture", () =>
{
    var result = projector.Project(CreateRequest(identityContext: CreateContext(
        authenticationMethod: AuthenticationMethod.MultiFactor,
        authenticationAssurance: AuthenticationAssurance.MultiFactor)));
    Require(result.Posture == CustomerSecurityCenterPosture.Review, "multi-factor non-passkey context should be review");
    Require(result.Signals.Contains("AUTH_MULTI_FACTOR"), "multi-factor signal required");
});

Check("SEC-030", "identity authority and security states are propagated exactly", () =>
{
    var result = projector.Project(CreateRequest(identityContext: CreateContext(
        deviceState: DeviceTrustState.Restricted,
        accountState: AccountSecurityState.Restricted,
        recoveryState: RecoveryState.RecoveredRestricted,
        authenticationMethod: AuthenticationMethod.MultiFactor,
        authenticationAssurance: AuthenticationAssurance.MultiFactor)));
    Require(result.IdentityAuthority == IdentityContextAuthority.ServerAuthoritative, "identity authority must remain server authoritative");
    Require(result.SessionState == SecuritySessionState.Active, "session state must propagate");
    Require(result.DeviceTrustState == DeviceTrustState.Restricted, "device state must propagate");
    Require(result.AccountSecurityState == AccountSecurityState.Restricted, "account state must propagate");
    Require(result.RecoveryState == RecoveryState.RecoveredRestricted, "recovery state must propagate");
});

Check("SEC-031", "projection authority is strictly read only", () =>
{
    var authority = projector.Project(CreateRequest()).Authority;
    Require(authority.ReadOnly, "security-center authority must be read only");
    Require(!authority.CanRevealSecrets, "security center cannot reveal secrets");
    Require(!authority.CanRevealRestrictedPii, "security center cannot reveal restricted PII");
    Require(!authority.CanRevokeSession, "security center cannot revoke sessions");
    Require(!authority.CanRevokeDevice, "security center cannot revoke devices");
    Require(!authority.CanCallIdentityProvider, "security center cannot call identity provider");
    Require(!authority.CanTrade, "security center cannot trade");
    Require(!authority.CanMoveMoney, "security center cannot move money");
});

Check("SEC-032", "snapshot schema exposes no secret token principal or restricted pii fields", () =>
{
    var forbiddenFragments = new[]
    {
        "Token",
        "Secret",
        "Credential",
        "Password",
        "Email",
        "Phone",
        "Address",
        "National",
        "Cnic",
        "PrincipalId",
        "Roles",
        "Scopes",
        "StepUp",
    };
    var names = typeof(CustomerSecurityCenterSnapshot).GetProperties()
        .Select(static property => property.Name)
        .ToArray();

    foreach (var fragment in forbiddenFragments)
    {
        Require(!names.Any(name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
            $"snapshot must not expose forbidden field fragment '{fragment}'");
    }
});

Check("SEC-033", "projection does not leak principal identity roles or scopes through signal values", () =>
{
    var context = CreateContext(
        principalId: "sensitive-principal-reference",
        scopes: new[] { "sensitive.scope" });
    var result = projector.Project(CreateRequest(identityContext: context));
    Require(!result.Signals.Any(signal => signal.Contains("sensitive", StringComparison.OrdinalIgnoreCase)),
        "signals must not contain principal/scopes");
    Require(!result.ToString().Contains("sensitive-principal-reference", StringComparison.Ordinal),
        "snapshot must not contain principal id");
    Require(!result.ToString().Contains("sensitive.scope", StringComparison.Ordinal),
        "snapshot must not contain scope values");
});

Check("SEC-034", "warning signal ordering is stable ordinal", () =>
{
    var result = projector.Project(CreateRequest(identityContext: CreateContext(
        deviceState: DeviceTrustState.Untrusted,
        recoveryState: RecoveryState.SecurityHold,
        authenticationMethod: AuthenticationMethod.Password,
        authenticationAssurance: AuthenticationAssurance.SingleFactor)));
    var sorted = result.Signals.OrderBy(static signal => signal, StringComparer.Ordinal).ToArray();
    Require(result.Signals.SequenceEqual(sorted), "signals must be stable ordinal order");
});

Check("SEC-035", "projector has no provider database or mutation dependency", () =>
{
    var constructors = typeof(DeterministicCustomerSecurityCenterProjector).GetConstructors();
    Require(constructors.Length == 1, "projector must expose exactly one public constructor");
    Require(constructors[0].GetParameters().Length == 0,
        "projector must not depend on provider/database/mutation infrastructure");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Customer security center verification FAILED ({failures.Count}):");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($" - {failure}");
    }

    return 1;
}

Console.WriteLine($"Customer security center verifier PASS ({passes}/35).");
Console.WriteLine("READ_ONLY: projection cannot revoke sessions/devices, call providers, trade or move money.");
Console.WriteLine("CUSTOMER_SCOPE: authenticated customer and server-authoritative principal customer scope must agree.");
Console.WriteLine("SAFE_OUTPUT: no secret, bearer/session token, credential, principal identity, role/scope or restricted-PII field is projected.");
Console.WriteLine("IDENTITY_PROPAGATION: session/device/account/recovery/authentication state is copied from server-authoritative identity context.");
Console.WriteLine("NOT_LIVE: no API, persistence, IdP/provider transport, pyPSX, trading, money movement or production path is exercised.");
return 0;

void Check(string id, string description, Action assertion)
{
    try
    {
        assertion();
        passes++;
        Console.WriteLine($"SECURITY_CENTER_PASS {id} {description}");
    }
    catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
    {
        failures.Add($"{id}: {exception.Message}");
    }
}

void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

void RequireThrows<TException>(Action action, string? expectedMessage = null)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException exception)
    {
        if (expectedMessage is not null)
        {
            Require(exception.Message.Contains(expectedMessage, StringComparison.Ordinal),
                $"expected exception containing '{expectedMessage}', received '{exception.Message}'");
        }

        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

CustomerSecurityCenterRequest CreateRequest(
    Guid? authenticatedCustomerId = null,
    Guid? requestCustomerId = null,
    string sessionReference = "SESSION-001",
    string deviceReference = "DEVICE-001",
    DateTimeOffset? requestObservedAt = null,
    IdentitySecurityContext? identityContext = null)
{
    var owner = requestCustomerId ?? customerId;
    return new CustomerSecurityCenterRequest(
        authenticatedCustomerId ?? owner,
        owner,
        sessionReference,
        deviceReference,
        requestObservedAt ?? observedAt,
        identityContext ?? CreateContext(principalCustomerId: owner.ToString("D")));
}

IdentitySecurityContext CreateContext(
    IdentityContextAuthority authority = IdentityContextAuthority.ServerAuthoritative,
    PrincipalType principalType = PrincipalType.Customer,
    string? principalId = "principal-001",
    string? principalCustomerId = null,
    IReadOnlyCollection<SecurityRole>? roles = null,
    IReadOnlyCollection<string>? scopes = null,
    SecuritySessionState sessionState = SecuritySessionState.Active,
    DeviceTrustState deviceState = DeviceTrustState.Trusted,
    AccountSecurityState accountState = AccountSecurityState.Normal,
    RecoveryState recoveryState = RecoveryState.Normal,
    AuthenticationMethod authenticationMethod = AuthenticationMethod.Passkey,
    AuthenticationAssurance authenticationAssurance = AuthenticationAssurance.PhishingResistant)
{
    var principal = new IdentityPrincipal(
        principalId ?? "principal-001",
        principalType,
        roles ?? new[] { SecurityRole.Investor },
        scopes ?? Array.Empty<string>(),
        principalCustomerId ?? customerId.ToString("D"));

    return new IdentitySecurityContext(
        authority,
        principal,
        sessionState,
        deviceState,
        accountState,
        recoveryState,
        authenticationMethod,
        authenticationAssurance,
        StepUpGrant: null);
}
