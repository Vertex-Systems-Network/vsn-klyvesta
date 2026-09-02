namespace Klyvesta.Domain.Identity;

public enum PrincipalType
{
    Unknown = 0,
    Customer = 1,
    Staff = 2,
    Service = 3,
    AiAgent = 4,
}

public enum SecurityRole
{
    Unknown = 0,
    Investor = 1,
    SupportL1 = 2,
    SupportL2 = 3,
    KycAnalyst = 4,
    AmlAnalyst = 5,
    ComplianceOfficer = 6,
    RiskAnalyst = 7,
    RiskApprover = 8,
    ReconciliationOperator = 9,
    ReconciliationApprover = 10,
    SecurityAnalyst = 11,
    PlatformAdmin = 12,
    Sre = 13,
    Auditor = 14,
    AiCoach = 15,
    ResearchAgent = 16,
    PortfolioAgent = 17,
    RebalanceAgent = 18,
    RiskGovernor = 19,
    ComplianceGate = 20,
    ExecutionValidator = 21,
    BrokerAdapter = 22,
    NotificationService = 23,
    ReconciliationWorker = 24,
}

public enum AuthenticationMethod
{
    Unknown = 0,
    Password = 1,
    MultiFactor = 2,
    Passkey = 3,
    WorkloadIdentity = 4,
}

public enum AuthenticationAssurance
{
    Unknown = 0,
    SingleFactor = 1,
    MultiFactor = 2,
    PhishingResistant = 3,
}

public enum SecuritySessionState
{
    Unknown = 0,
    Active = 1,
    Revoked = 2,
    Expired = 3,
}

public enum DeviceTrustState
{
    Unknown = 0,
    Trusted = 1,
    Untrusted = 2,
    Restricted = 3,
    Revoked = 4,
}

public enum AccountSecurityState
{
    Unknown = 0,
    Normal = 1,
    Restricted = 2,
    Suspended = 3,
    Closed = 4,
}

public enum RecoveryState
{
    Unknown = 0,
    Normal = 1,
    RecoveryStarted = 2,
    IdentityReverification = 3,
    SecurityHold = 4,
    RecoveredRestricted = 5,
}

public enum IdentityContextAuthority
{
    Unknown = 0,
    ServerAuthoritative = 1,
}

public sealed class IdentityPrincipal
{
    private readonly HashSet<SecurityRole> _roles;
    private readonly HashSet<string> _scopes;

    public IdentityPrincipal(
        string principalId,
        PrincipalType principalType,
        IEnumerable<SecurityRole> roles,
        IEnumerable<string>? scopes = null,
        string? customerId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);
        ArgumentNullException.ThrowIfNull(roles);

        PrincipalId = principalId;
        PrincipalType = principalType;
        CustomerId = customerId;
        _roles = new HashSet<SecurityRole>(roles);
        _scopes = new HashSet<string>(scopes ?? [], StringComparer.Ordinal);
    }

    public string PrincipalId { get; }

    public PrincipalType PrincipalType { get; }

    public string? CustomerId { get; }

    public IReadOnlySet<SecurityRole> Roles => _roles;

    public IReadOnlySet<string> Scopes => _scopes;

    public bool HasRole(SecurityRole role) => role != SecurityRole.Unknown && _roles.Contains(role);

    public bool HasScope(string scope) => !string.IsNullOrWhiteSpace(scope) && _scopes.Contains(scope);
}

public sealed record StepUpGrant(
    string Action,
    AuthenticationAssurance Assurance,
    DateTimeOffset AuthenticatedAt,
    DateTimeOffset ExpiresAt)
{
    public bool IsValidFor(string action, AuthenticationAssurance requiredAssurance, DateTimeOffset now)
    {
        return !string.IsNullOrWhiteSpace(action)
            && string.Equals(Action, action, StringComparison.Ordinal)
            && requiredAssurance != AuthenticationAssurance.Unknown
            && Assurance >= requiredAssurance
            && AuthenticatedAt <= now
            && ExpiresAt > now;
    }
}

public sealed record IdentitySecurityContext(
    IdentityContextAuthority Authority,
    IdentityPrincipal? Principal,
    SecuritySessionState SessionState,
    DeviceTrustState DeviceTrustState,
    AccountSecurityState AccountSecurityState,
    RecoveryState RecoveryState,
    AuthenticationMethod AuthenticationMethod,
    AuthenticationAssurance AuthenticationAssurance,
    StepUpGrant? StepUpGrant)
{
    public bool IsAuthenticated =>
        Authority == IdentityContextAuthority.ServerAuthoritative
        && Principal is not null
        && Principal.PrincipalType != PrincipalType.Unknown
        && SessionState == SecuritySessionState.Active;

    public AccountSecurityState EffectiveAccountSecurityState => RecoveryState switch
    {
        RecoveryState.Normal => AccountSecurityState,
        RecoveryState.RecoveryStarted or RecoveryState.IdentityReverification or RecoveryState.SecurityHold or RecoveryState.RecoveredRestricted
            => AccountSecurityState.Restricted,
        _ => AccountSecurityState.Unknown,
    };

    public static IdentitySecurityContext FailClosedUnauthenticated => new(
        IdentityContextAuthority.ServerAuthoritative,
        Principal: null,
        SecuritySessionState.Unknown,
        DeviceTrustState.Unknown,
        AccountSecurityState.Unknown,
        RecoveryState.Unknown,
        AuthenticationMethod.Unknown,
        AuthenticationAssurance.Unknown,
        StepUpGrant: null);
}
