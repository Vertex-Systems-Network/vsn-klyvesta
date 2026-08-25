namespace Klyvesta.Domain.Security;

public enum ProtectedApprovalState
{
    Pending,
    Approved,
    Expired,
}

public sealed class ProtectedApproval
{
    public ProtectedApproval(
        Guid proposalId,
        SecurityAction action,
        Guid proposedByPrincipalId,
        SecurityRole requiredApproverRole,
        DateTimeOffset expiresAt)
    {
        if (proposalId == Guid.Empty)
        {
            throw new ArgumentException("Proposal identifier must be non-empty.", nameof(proposalId));
        }

        if (proposedByPrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Proposer identifier must be non-empty.", nameof(proposedByPrincipalId));
        }

        var expectedRole = GetExpectedApproverRole(action);
        if (requiredApproverRole != expectedRole)
        {
            throw new ArgumentException(
                $"Action {action} requires approver role {expectedRole}.",
                nameof(requiredApproverRole));
        }

        ProposalId = proposalId;
        Action = action;
        ProposedByPrincipalId = proposedByPrincipalId;
        RequiredApproverRole = requiredApproverRole;
        ExpiresAt = expiresAt;
    }

    public Guid ProposalId { get; }

    public SecurityAction Action { get; }

    public Guid ProposedByPrincipalId { get; }

    public SecurityRole RequiredApproverRole { get; }

    public DateTimeOffset ExpiresAt { get; }

    public ProtectedApprovalState State { get; private set; } = ProtectedApprovalState.Pending;

    public Guid? ApprovedByPrincipalId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public SecurityDecision TryApprove(SecurityPrincipal approver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(approver);

        if (State is not ProtectedApprovalState.Pending)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        if (now >= ExpiresAt)
        {
            State = ProtectedApprovalState.Expired;
            return SecurityDecision.Deny(SecurityDenialReason.ApprovalExpired);
        }

        if (approver.Type is not PrincipalType.Staff || approver.Role != RequiredApproverRole)
        {
            return SecurityDecision.Deny(SecurityDenialReason.RoleDenied);
        }

        if (approver.PrincipalId == Guid.Empty)
        {
            return SecurityDecision.Deny(SecurityDenialReason.AuthRequired);
        }

        if (approver.PrincipalId == ProposedByPrincipalId)
        {
            return SecurityDecision.Deny(SecurityDenialReason.MakerCheckerViolation);
        }

        State = ProtectedApprovalState.Approved;
        ApprovedByPrincipalId = approver.PrincipalId;
        ApprovedAt = now;
        return SecurityDecision.Allow();
    }

    private static SecurityRole GetExpectedApproverRole(SecurityAction action) => action switch
    {
        SecurityAction.ApproveFinancialCorrection => SecurityRole.ReconciliationApprover,
        SecurityAction.ApproveRiskPolicyChange => SecurityRole.RiskApprover,
        _ => throw new ArgumentOutOfRangeException(
            nameof(action),
            action,
            "The action is not configured for protected maker-checker approval."),
    };
}
