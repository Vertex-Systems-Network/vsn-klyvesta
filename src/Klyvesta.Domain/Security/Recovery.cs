namespace Klyvesta.Domain.Security;

public enum RecoveryState
{
    Normal,
    RecoveryStarted,
    IdentityReverification,
    SecurityHold,
    RecoveredRestricted,
}

public sealed class RecoverySecurityState
{
    public RecoveryState State { get; private set; } = RecoveryState.Normal;

    public DateTimeOffset? RecoveryStartedAt { get; private set; }

    public DateTimeOffset? RestrictionUntil { get; private set; }

    public SecurityDecision StartRecovery(DateTimeOffset now)
    {
        if (State is not RecoveryState.Normal)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        State = RecoveryState.RecoveryStarted;
        RecoveryStartedAt = now;
        RestrictionUntil = null;
        return SecurityDecision.Allow();
    }

    public SecurityDecision BeginIdentityReverification()
    {
        if (State is not RecoveryState.RecoveryStarted)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        State = RecoveryState.IdentityReverification;
        return SecurityDecision.Allow();
    }

    public SecurityDecision PlaceSecurityHold()
    {
        if (State is not RecoveryState.IdentityReverification)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        State = RecoveryState.SecurityHold;
        return SecurityDecision.Allow();
    }

    public SecurityDecision MarkRecoveredRestricted(DateTimeOffset now, TimeSpan minimumRestriction)
    {
        if (State is not RecoveryState.SecurityHold || minimumRestriction <= TimeSpan.Zero)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        State = RecoveryState.RecoveredRestricted;
        RestrictionUntil = now.Add(minimumRestriction);
        return SecurityDecision.Allow();
    }

    public SecurityDecision ExtendRestriction(DateTimeOffset restrictionUntil)
    {
        if (State is not RecoveryState.RecoveredRestricted || RestrictionUntil is null || restrictionUntil <= RestrictionUntil)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        RestrictionUntil = restrictionUntil;
        return SecurityDecision.Allow();
    }

    public SecurityDecision TryClearRestriction(DateTimeOffset now, bool riskReviewSatisfied)
    {
        if (State is not RecoveryState.RecoveredRestricted || RestrictionUntil is null)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        if (!riskReviewSatisfied || now < RestrictionUntil)
        {
            return SecurityDecision.Deny(SecurityDenialReason.SecurityHold);
        }

        State = RecoveryState.Normal;
        RestrictionUntil = null;
        return SecurityDecision.Allow();
    }

    public bool AllowsHighRiskActions() => State is RecoveryState.Normal;
}
