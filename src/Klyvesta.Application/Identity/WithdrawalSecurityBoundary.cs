using Klyvesta.Domain.Identity;

namespace Klyvesta.Application.Identity;

public enum BeneficiarySecurityState
{
    Unknown = 0,
    PendingVerification = 1,
    Verified = 2,
    Revoked = 3,
}

public sealed record WithdrawalBeneficiaryEvidence(
    string BeneficiaryReference,
    string CustomerId,
    BeneficiarySecurityState State,
    DateTimeOffset AvailableAfter);

public sealed record WithdrawalSecurityRequest(
    string AccountReference,
    string AccountCustomerId,
    WithdrawalBeneficiaryEvidence Beneficiary,
    decimal Amount,
    string Currency);

public sealed record WithdrawalSecurityDecision(bool IsAllowed, string ReasonCode);

public sealed class WithdrawalSecurityEvaluator
{
    private readonly IIdentityAuthorizationEvaluator _authorizationEvaluator;

    public WithdrawalSecurityEvaluator(IIdentityAuthorizationEvaluator authorizationEvaluator)
    {
        _authorizationEvaluator = authorizationEvaluator ?? throw new ArgumentNullException(nameof(authorizationEvaluator));
    }

    public WithdrawalSecurityDecision Evaluate(
        IdentitySecurityContext context,
        WithdrawalSecurityRequest request,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Beneficiary);

        if (string.IsNullOrWhiteSpace(request.AccountReference) ||
            string.IsNullOrWhiteSpace(request.AccountCustomerId) ||
            string.IsNullOrWhiteSpace(request.Beneficiary.BeneficiaryReference) ||
            string.IsNullOrWhiteSpace(request.Beneficiary.CustomerId) ||
            string.IsNullOrWhiteSpace(request.Currency) ||
            request.Amount <= 0m)
        {
            return Deny("WITHDRAWAL_REQUEST_INVALID");
        }

        var identityDecision = _authorizationEvaluator.Evaluate(
            context,
            new IdentityAuthorizationRequest(
                IdentityAuthorizationAction.WithdrawalRequest,
                new ResolvedAuthorizationResource(request.AccountReference, request.AccountCustomerId)),
            evaluatedAt);

        if (!identityDecision.IsIdentityAuthorized)
        {
            return Deny(identityDecision.ReasonCode);
        }

        if (!StringComparer.Ordinal.Equals(request.Beneficiary.CustomerId, request.AccountCustomerId))
        {
            return Deny("RESOURCE_NOT_FOUND_OR_FORBIDDEN");
        }

        if (request.Beneficiary.State != BeneficiarySecurityState.Verified)
        {
            return Deny("BENEFICIARY_UNVERIFIED");
        }

        if (evaluatedAt < request.Beneficiary.AvailableAfter)
        {
            return Deny("BENEFICIARY_COOLING_OFF");
        }

        return new WithdrawalSecurityDecision(true, "WITHDRAWAL_SECURITY_APPROVED");
    }

    private static WithdrawalSecurityDecision Deny(string reasonCode) => new(false, reasonCode);
}

public sealed record ManagedSecuritySession(
    string SessionReference,
    string PrincipalId,
    string? CustomerId,
    string DeviceReference,
    DeviceTrustState DeviceTrustState,
    SecuritySessionState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed class SessionDeviceSecurityInventory
{
    private readonly object _sync = new();
    private readonly Dictionary<string, ManagedSecuritySession> _sessions = new(StringComparer.Ordinal);

    public ManagedSecuritySession Register(
        string sessionReference,
        string deviceReference,
        IdentitySecurityContext context,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceReference);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsAuthenticated || context.Principal is null)
        {
            throw new InvalidOperationException("Only a server-authoritative authenticated identity context may register a session.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, createdAt);

        var session = new ManagedSecuritySession(
            sessionReference,
            context.Principal.PrincipalId,
            context.Principal.CustomerId,
            deviceReference,
            context.DeviceTrustState,
            SecuritySessionState.Active,
            createdAt,
            expiresAt);

        lock (_sync)
        {
            if (_sessions.TryGetValue(sessionReference, out var existing))
            {
                if (existing == session)
                {
                    return existing;
                }

                throw new InvalidOperationException("Session reference conflicts with different server-authoritative evidence.");
            }

            _sessions.Add(sessionReference, session);
        }

        return session;
    }

    public ManagedSecuritySession RevokeSession(string sessionReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionReference);
        lock (_sync)
        {
            var session = GetLocked(sessionReference);
            var revoked = session with { State = SecuritySessionState.Revoked };
            _sessions[sessionReference] = revoked;
            return revoked;
        }
    }

    public int RevokeDevice(string principalId, string deviceReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceReference);

        lock (_sync)
        {
            var matches = _sessions.Values
                .Where(session =>
                    StringComparer.Ordinal.Equals(session.PrincipalId, principalId) &&
                    StringComparer.Ordinal.Equals(session.DeviceReference, deviceReference) &&
                    session.State == SecuritySessionState.Active)
                .Select(session => session.SessionReference)
                .ToArray();

            foreach (var reference in matches)
            {
                _sessions[reference] = _sessions[reference] with
                {
                    State = SecuritySessionState.Revoked,
                    DeviceTrustState = DeviceTrustState.Revoked,
                };
            }

            return matches.Length;
        }
    }

    public int SignOutAll(string principalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);
        lock (_sync)
        {
            var matches = _sessions.Values
                .Where(session =>
                    StringComparer.Ordinal.Equals(session.PrincipalId, principalId) &&
                    session.State == SecuritySessionState.Active)
                .Select(session => session.SessionReference)
                .ToArray();

            foreach (var reference in matches)
            {
                _sessions[reference] = _sessions[reference] with { State = SecuritySessionState.Revoked };
            }

            return matches.Length;
        }
    }

    public IReadOnlyList<ManagedSecuritySession> GetActiveSessions(string principalId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);
        lock (_sync)
        {
            return _sessions.Values
                .Where(session =>
                    StringComparer.Ordinal.Equals(session.PrincipalId, principalId) &&
                    session.State == SecuritySessionState.Active &&
                    session.ExpiresAt > now)
                .OrderBy(session => session.CreatedAt)
                .ThenBy(session => session.SessionReference, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public ManagedSecuritySession Get(string sessionReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionReference);
        lock (_sync)
        {
            return GetLocked(sessionReference);
        }
    }

    private ManagedSecuritySession GetLocked(string sessionReference) =>
        _sessions.TryGetValue(sessionReference, out var session)
            ? session
            : throw new KeyNotFoundException($"Security session {sessionReference} does not exist.");
}

public sealed record BreakGlassProposal(
    Guid ProposalId,
    string MakerPrincipalId,
    string Action,
    string Purpose,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record BreakGlassGrant(
    Guid ProposalId,
    string MakerPrincipalId,
    string ApproverPrincipalId,
    string Action,
    DateTimeOffset ApprovedAt,
    DateTimeOffset ExpiresAt)
{
    public bool IsValidFor(string principalId, string action, DateTimeOffset now) =>
        StringComparer.Ordinal.Equals(MakerPrincipalId, principalId) &&
        StringComparer.Ordinal.Equals(Action, action) &&
        ApprovedAt <= now &&
        ExpiresAt > now;
}

public sealed class BreakGlassApprovalService
{
    public const string ApprovalStepUpAction = "identity.breakglass.approve";

    private readonly TimeSpan _maximumProposalLifetime;
    private readonly TimeSpan _maximumGrantLifetime;

    public BreakGlassApprovalService(
        TimeSpan? maximumProposalLifetime = null,
        TimeSpan? maximumGrantLifetime = null)
    {
        _maximumProposalLifetime = maximumProposalLifetime ?? TimeSpan.FromMinutes(30);
        _maximumGrantLifetime = maximumGrantLifetime ?? TimeSpan.FromMinutes(15);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_maximumProposalLifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_maximumGrantLifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_maximumGrantLifetime, _maximumProposalLifetime);
    }

    public BreakGlassProposal CreateProposal(
        string makerPrincipalId,
        string action,
        string purpose,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(makerPrincipalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, createdAt);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expiresAt - createdAt, _maximumProposalLifetime);

        return new BreakGlassProposal(Guid.NewGuid(), makerPrincipalId, action, purpose, createdAt, expiresAt);
    }

    public BreakGlassGrant Approve(
        BreakGlassProposal proposal,
        IdentitySecurityContext approverContext,
        DateTimeOffset approvedAt)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(approverContext);

        if (approvedAt >= proposal.ExpiresAt)
        {
            throw new InvalidOperationException("Expired break-glass proposal cannot be approved.");
        }

        if (!approverContext.IsAuthenticated || approverContext.Principal is null)
        {
            throw new InvalidOperationException("Authenticated approver is required.");
        }

        var principal = approverContext.Principal;
        if (StringComparer.Ordinal.Equals(principal.PrincipalId, proposal.MakerPrincipalId))
        {
            throw new InvalidOperationException("Break-glass maker cannot self-approve.");
        }

        if (principal.PrincipalType != PrincipalType.Staff ||
            !principal.HasRole(SecurityRole.SecurityAnalyst) ||
            !principal.HasScope("security.breakglass.approve"))
        {
            throw new InvalidOperationException("Security analyst approval scope is required.");
        }

        if (approverContext.DeviceTrustState != DeviceTrustState.Trusted ||
            approverContext.AuthenticationAssurance < AuthenticationAssurance.PhishingResistant ||
            approverContext.StepUpGrant is null ||
            !approverContext.StepUpGrant.IsValidFor(
                ApprovalStepUpAction,
                AuthenticationAssurance.PhishingResistant,
                approvedAt))
        {
            throw new InvalidOperationException("Trusted-device phishing-resistant step-up is required for break-glass approval.");
        }

        var grantExpiry = approvedAt + _maximumGrantLifetime;
        if (grantExpiry > proposal.ExpiresAt)
        {
            grantExpiry = proposal.ExpiresAt;
        }

        return new BreakGlassGrant(
            proposal.ProposalId,
            proposal.MakerPrincipalId,
            principal.PrincipalId,
            proposal.Action,
            approvedAt,
            grantExpiry);
    }
}
