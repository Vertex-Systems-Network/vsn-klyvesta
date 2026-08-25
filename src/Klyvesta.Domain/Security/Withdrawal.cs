namespace Klyvesta.Domain.Security;

public enum AuthenticationStrength
{
    Unknown = 0,
    Password = 1,
    StrongMfa = 2,
    PhishingResistant = 3,
}

public sealed class StepUpGrant
{
    private StepUpGrant(
        Guid principalId,
        Guid sessionId,
        SecurityAction action,
        AuthenticationStrength strength,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        PrincipalId = principalId;
        SessionId = sessionId;
        Action = action;
        Strength = strength;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public Guid PrincipalId { get; }

    public Guid SessionId { get; }

    public SecurityAction Action { get; }

    public AuthenticationStrength Strength { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public static StepUpGrant Create(
        Guid principalId,
        Guid sessionId,
        SecurityAction action,
        AuthenticationStrength strength,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        if (principalId == Guid.Empty)
        {
            throw new ArgumentException("Principal identifier must be non-empty.", nameof(principalId));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session identifier must be non-empty.", nameof(sessionId));
        }

        if (strength is AuthenticationStrength.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), strength, "Authentication strength must be known.");
        }

        if (expiresAt <= issuedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), expiresAt, "Step-up expiry must be after issuance.");
        }

        return new StepUpGrant(principalId, sessionId, action, strength, issuedAt, expiresAt);
    }

    public bool IsValidFor(
        Guid principalId,
        Guid sessionId,
        SecurityAction action,
        AuthenticationStrength minimumStrength,
        DateTimeOffset now) =>
        PrincipalId == principalId &&
        SessionId == sessionId &&
        Action == action &&
        Strength >= minimumStrength &&
        now >= IssuedAt &&
        now < ExpiresAt;
}

public enum BeneficiaryVerificationStatus
{
    Unverified,
    Verified,
    Revoked,
}

public sealed record WithdrawalBeneficiary(
    Guid BeneficiaryId,
    Guid CustomerId,
    BeneficiaryVerificationStatus VerificationStatus,
    DateTimeOffset AvailableAfter);

public sealed record WithdrawalSecurityContext(
    SecurityPrincipal Principal,
    Guid SessionId,
    Guid AccountCustomerId,
    bool IsAuthenticated,
    SecuritySessionState SessionState,
    AccountStatus AccountStatus,
    bool ComplianceHold,
    bool SecurityHold,
    bool NewOrRecoveredDeviceRestricted,
    RecoverySecurityState Recovery,
    WithdrawalBeneficiary Beneficiary,
    StepUpGrant? StepUpGrant,
    DateTimeOffset Now);

public static class WithdrawalSecurityPolicy
{
    public static SecurityDecision Evaluate(WithdrawalSecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var authorizationDecision = AuthorizationEvaluator.Evaluate(
            context.Principal,
            new AuthorizationRequest(
                SecurityAction.RequestWithdrawal,
                context.AccountCustomerId,
                context.IsAuthenticated,
                context.SessionState,
                context.AccountStatus,
                context.ComplianceHold,
                context.SecurityHold));

        if (!authorizationDecision.Allowed)
        {
            return authorizationDecision;
        }

        if (context.Beneficiary.CustomerId != context.AccountCustomerId)
        {
            return SecurityDecision.Deny(SecurityDenialReason.ResourceNotFoundOrForbidden);
        }

        if (context.Beneficiary.VerificationStatus is not BeneficiaryVerificationStatus.Verified)
        {
            return SecurityDecision.Deny(SecurityDenialReason.BeneficiaryUnverified);
        }

        if (context.Now < context.Beneficiary.AvailableAfter)
        {
            return SecurityDecision.Deny(SecurityDenialReason.BeneficiaryCoolingOff);
        }

        if (context.NewOrRecoveredDeviceRestricted || !context.Recovery.AllowsHighRiskActions())
        {
            return SecurityDecision.Deny(SecurityDenialReason.SecurityHold);
        }

        if (context.StepUpGrant is null ||
            !context.StepUpGrant.IsValidFor(
                context.Principal.PrincipalId,
                context.SessionId,
                SecurityAction.RequestWithdrawal,
                AuthenticationStrength.StrongMfa,
                context.Now))
        {
            return SecurityDecision.Deny(SecurityDenialReason.StepUpRequired);
        }

        return SecurityDecision.Allow();
    }
}
