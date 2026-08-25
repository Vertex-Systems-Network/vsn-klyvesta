namespace Klyvesta.Domain.Security;

public enum PrincipalType
{
    Customer,
    Staff,
    Service,
}

public enum SecurityRole
{
    Investor,
    SupportL1,
    SupportL2,
    KycAnalyst,
    AmlAnalyst,
    ComplianceOfficer,
    RiskAnalyst,
    RiskApprover,
    ReconciliationOperator,
    ReconciliationApprover,
    SecurityAnalyst,
    PlatformAdmin,
    Sre,
    Auditor,
    AiCoach,
    ResearchAgent,
    PortfolioAgent,
    RebalanceAgent,
    RiskGovernor,
    ComplianceGate,
    ExecutionValidator,
    BrokerAdapter,
    NotificationService,
    ReconciliationWorker,
}

public enum SecurityAction
{
    ReadCustomerProfile,
    ReadPortfolio,
    CreateManualOrderIntent,
    ApproveAiRecommendation,
    ManageAutoMandate,
    PauseAutoMandate,
    RequestWithdrawal,
    ChangeBeneficiary,
    RevokeSessions,
    FreezeSecurityState,
    ProposeFinancialCorrection,
    ApproveFinancialCorrection,
    ProposeRiskPolicyChange,
    ApproveRiskPolicyChange,
    ModifyLedgerHistory,
    ChangeCustomerBankDestination,
    SubmitBrokerOrder,
    WriteAuthoritativeLedger,
}

public enum SecuritySessionState
{
    Active,
    Restricted,
    Revoked,
    Expired,
}

public enum AccountStatus
{
    Active,
    Restricted,
    Suspended,
    Closed,
}

public enum SecurityDenialReason
{
    None,
    AuthRequired,
    ResourceNotFoundOrForbidden,
    RoleDenied,
    AccountRestricted,
    ComplianceHold,
    SecurityHold,
    StepUpRequired,
    BeneficiaryUnverified,
    BeneficiaryCoolingOff,
    FeatureNotEntitled,
    FeatureNotLegallyAvailable,
    InvalidStateTransition,
    MakerCheckerViolation,
    ApprovalExpired,
}

public readonly record struct SecurityDecision(bool Allowed, SecurityDenialReason Reason)
{
    public static SecurityDecision Allow() => new(true, SecurityDenialReason.None);

    public static SecurityDecision Deny(SecurityDenialReason reason) => new(false, reason);
}

public sealed record SecurityPrincipal(
    Guid PrincipalId,
    PrincipalType Type,
    SecurityRole Role,
    Guid? CustomerId,
    IReadOnlySet<string> Entitlements);

public sealed record AuthorizationRequest(
    SecurityAction Action,
    Guid? ResourceCustomerId,
    bool IsAuthenticated,
    SecuritySessionState SessionState,
    AccountStatus AccountStatus,
    bool ComplianceHold,
    bool SecurityHold,
    string? RequiredEntitlement = null,
    bool LegalFeatureEnabled = true);

public static class AuthorizationEvaluator
{
    public static SecurityDecision Evaluate(SecurityPrincipal principal, AuthorizationRequest request)
    {
        if (!request.IsAuthenticated || request.SessionState is SecuritySessionState.Revoked or SecuritySessionState.Expired)
        {
            return SecurityDecision.Deny(SecurityDenialReason.AuthRequired);
        }

        if (request.SessionState is SecuritySessionState.Restricted && IsHighRiskAction(request.Action))
        {
            return SecurityDecision.Deny(SecurityDenialReason.SecurityHold);
        }

        if (!IsRoleAllowed(principal.Role, request.Action))
        {
            return SecurityDecision.Deny(SecurityDenialReason.RoleDenied);
        }

        if (principal.Type is PrincipalType.Customer && RequiresCustomerOwnership(request.Action))
        {
            if (principal.CustomerId is null || request.ResourceCustomerId is null || principal.CustomerId != request.ResourceCustomerId)
            {
                return SecurityDecision.Deny(SecurityDenialReason.ResourceNotFoundOrForbidden);
            }
        }

        if (IsFinancialAction(request.Action) && request.AccountStatus is not AccountStatus.Active)
        {
            return SecurityDecision.Deny(SecurityDenialReason.AccountRestricted);
        }

        if (IsFinancialAction(request.Action) && request.ComplianceHold)
        {
            return SecurityDecision.Deny(SecurityDenialReason.ComplianceHold);
        }

        if (IsHighRiskAction(request.Action) && request.SecurityHold)
        {
            return SecurityDecision.Deny(SecurityDenialReason.SecurityHold);
        }

        if (!request.LegalFeatureEnabled)
        {
            return SecurityDecision.Deny(SecurityDenialReason.FeatureNotLegallyAvailable);
        }

        if (request.RequiredEntitlement is not null && !principal.Entitlements.Contains(request.RequiredEntitlement))
        {
            return SecurityDecision.Deny(SecurityDenialReason.FeatureNotEntitled);
        }

        return SecurityDecision.Allow();
    }

    private static bool IsRoleAllowed(SecurityRole role, SecurityAction action) => role switch
    {
        SecurityRole.Investor => action is
            SecurityAction.ReadCustomerProfile or
            SecurityAction.ReadPortfolio or
            SecurityAction.CreateManualOrderIntent or
            SecurityAction.ApproveAiRecommendation or
            SecurityAction.ManageAutoMandate or
            SecurityAction.PauseAutoMandate or
            SecurityAction.RequestWithdrawal or
            SecurityAction.ChangeBeneficiary,

        SecurityRole.SecurityAnalyst => action is
            SecurityAction.RevokeSessions or
            SecurityAction.FreezeSecurityState,

        SecurityRole.RiskAnalyst => action is SecurityAction.ProposeRiskPolicyChange,
        SecurityRole.RiskApprover => action is SecurityAction.ApproveRiskPolicyChange,
        SecurityRole.ReconciliationOperator => action is SecurityAction.ProposeFinancialCorrection,
        SecurityRole.ReconciliationApprover => action is SecurityAction.ApproveFinancialCorrection,
        SecurityRole.ExecutionValidator => action is SecurityAction.SubmitBrokerOrder,
        SecurityRole.BrokerAdapter => action is SecurityAction.SubmitBrokerOrder,
        _ => false,
    };

    private static bool RequiresCustomerOwnership(SecurityAction action) => action is
        SecurityAction.ReadCustomerProfile or
        SecurityAction.ReadPortfolio or
        SecurityAction.CreateManualOrderIntent or
        SecurityAction.ApproveAiRecommendation or
        SecurityAction.ManageAutoMandate or
        SecurityAction.PauseAutoMandate or
        SecurityAction.RequestWithdrawal or
        SecurityAction.ChangeBeneficiary;

    private static bool IsFinancialAction(SecurityAction action) => action is
        SecurityAction.CreateManualOrderIntent or
        SecurityAction.ApproveAiRecommendation or
        SecurityAction.ManageAutoMandate or
        SecurityAction.PauseAutoMandate or
        SecurityAction.RequestWithdrawal or
        SecurityAction.ChangeBeneficiary or
        SecurityAction.ProposeFinancialCorrection or
        SecurityAction.ApproveFinancialCorrection or
        SecurityAction.SubmitBrokerOrder or
        SecurityAction.WriteAuthoritativeLedger;

    private static bool IsHighRiskAction(SecurityAction action) => action is
        SecurityAction.RequestWithdrawal or
        SecurityAction.ChangeBeneficiary or
        SecurityAction.ManageAutoMandate or
        SecurityAction.RevokeSessions or
        SecurityAction.FreezeSecurityState or
        SecurityAction.ApproveFinancialCorrection or
        SecurityAction.ApproveRiskPolicyChange or
        SecurityAction.SubmitBrokerOrder or
        SecurityAction.WriteAuthoritativeLedger;
}
