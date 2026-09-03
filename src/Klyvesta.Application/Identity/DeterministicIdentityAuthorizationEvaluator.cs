using Klyvesta.Domain.Identity;

namespace Klyvesta.Application.Identity;

public enum IdentityAuthorizationAction
{
    Unknown = 0,
    CustomerProfileRead = 1,
    PortfolioRead = 2,
    ManualOrderCreate = 3,
    AiRecommendationApprove = 4,
    WithdrawalRequest = 5,
    BeneficiaryManage = 6,
    SessionDeviceRevoke = 7,
    RiskPolicyPropose = 8,
    RiskPolicyApprove = 9,
    FinancialCorrectionPropose = 10,
    FinancialCorrectionApprove = 11,
    DeployApprovedArtifact = 12,
    PortfolioProposalCreate = 13,
    RebalanceProposalCreate = 14,
    RiskDecisionEvaluate = 15,
    ComplianceDecisionEvaluate = 16,
    AuthorizedBrokerOrderSubmit = 17,
    BrokerOperationExecute = 18,
    SanitizedNotificationSend = 19,
}

public enum IdentityAuthorizationOutcome
{
    Deny = 0,
    Allow = 1,
}

public sealed record ResolvedAuthorizationResource(
    string ResourceReference,
    string? OwnerCustomerId = null);

public sealed record IdentityAuthorizationRequest(
    IdentityAuthorizationAction Action,
    ResolvedAuthorizationResource Resource);

public sealed record IdentityAuthorizationDecision(
    IdentityAuthorizationOutcome Outcome,
    string ReasonCode,
    string PolicyVersion,
    IdentityAuthorizationAction Action,
    string? PrincipalReference)
{
    public bool IsIdentityAuthorized => Outcome == IdentityAuthorizationOutcome.Allow;
}

public interface IIdentityAuthorizationEvaluator
{
    IdentityAuthorizationDecision Evaluate(
        IdentitySecurityContext context,
        IdentityAuthorizationRequest request,
        DateTimeOffset evaluatedAt);
}

public static class IdentityAuthorizationActionNames
{
    public static string StepUpKey(IdentityAuthorizationAction action) =>
        $"identity.{action.ToString().ToLowerInvariant()}";
}

public sealed class DeterministicIdentityAuthorizationEvaluator : IIdentityAuthorizationEvaluator
{
    public const string CurrentPolicyVersion = "identity-authz-v1";

    private static readonly Dictionary<IdentityAuthorizationAction, AuthorizationRule> Rules = BuildRules();

    public IdentityAuthorizationDecision Evaluate(
        IdentitySecurityContext context,
        IdentityAuthorizationRequest request,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Resource);

        if (request.Action == IdentityAuthorizationAction.Unknown ||
            string.IsNullOrWhiteSpace(request.Resource.ResourceReference))
        {
            return Deny(request, null, "IDENTITY_AUTHZ_INVALID_REQUEST");
        }

        if (!context.IsAuthenticated ||
            context.Principal is null ||
            context.AuthenticationMethod == AuthenticationMethod.Unknown ||
            context.AuthenticationAssurance == AuthenticationAssurance.Unknown)
        {
            return Deny(request, context.Principal?.PrincipalId, "AUTH_REQUIRED");
        }

        var principal = context.Principal;
        if (!RolesAreCompatible(principal))
        {
            return Deny(request, principal.PrincipalId, "IDENTITY_PRINCIPAL_ROLE_INVALID");
        }

        if (!Rules.TryGetValue(request.Action, out var rule))
        {
            return Deny(request, principal.PrincipalId, "IDENTITY_AUTHZ_DENY");
        }

        var grant = rule.Grants.FirstOrDefault(candidate =>
            candidate.PrincipalType == principal.PrincipalType && principal.HasRole(candidate.Role));
        if (grant is null)
        {
            return Deny(request, principal.PrincipalId, "IDENTITY_AUTHZ_DENY");
        }

        if (!string.IsNullOrWhiteSpace(grant.RequiredScope) &&
            !principal.HasScope(grant.RequiredScope))
        {
            return Deny(request, principal.PrincipalId, "IDENTITY_SCOPE_REQUIRED");
        }

        if (grant.RequiresCustomerOwnership &&
            (string.IsNullOrWhiteSpace(principal.CustomerId) ||
             string.IsNullOrWhiteSpace(request.Resource.OwnerCustomerId) ||
             !StringComparer.Ordinal.Equals(principal.CustomerId, request.Resource.OwnerCustomerId)))
        {
            return Deny(request, principal.PrincipalId, "RESOURCE_NOT_FOUND_OR_FORBIDDEN");
        }

        if (rule.RequiresNormalRecovery && context.RecoveryState != RecoveryState.Normal)
        {
            return Deny(request, principal.PrincipalId, "SECURITY_HOLD");
        }

        if (rule.RequiresNormalAccount &&
            context.EffectiveAccountSecurityState != AccountSecurityState.Normal)
        {
            return Deny(request, principal.PrincipalId, "ACCOUNT_RESTRICTED");
        }

        if (!DeviceRequirementSatisfied(rule.DeviceRequirement, context.DeviceTrustState))
        {
            return Deny(
                request,
                principal.PrincipalId,
                context.DeviceTrustState == DeviceTrustState.Revoked
                    ? "SECURITY_DEVICE_REVOKED"
                    : "SECURITY_DEVICE_NOT_TRUSTED");
        }

        if (rule.RequiredStepUpAssurance != AuthenticationAssurance.Unknown &&
            (context.AuthenticationAssurance < rule.RequiredStepUpAssurance ||
             context.StepUpGrant is null ||
             !context.StepUpGrant.IsValidFor(
                 IdentityAuthorizationActionNames.StepUpKey(request.Action),
                 rule.RequiredStepUpAssurance,
                 evaluatedAt)))
        {
            return Deny(request, principal.PrincipalId, "STEP_UP_REQUIRED");
        }

        return new IdentityAuthorizationDecision(
            IdentityAuthorizationOutcome.Allow,
            "IDENTITY_AUTHZ_ALLOW",
            CurrentPolicyVersion,
            request.Action,
            principal.PrincipalId);
    }

    private static IdentityAuthorizationDecision Deny(
        IdentityAuthorizationRequest request,
        string? principalReference,
        string reasonCode) =>
        new(
            IdentityAuthorizationOutcome.Deny,
            reasonCode,
            CurrentPolicyVersion,
            request.Action,
            principalReference);

    private static bool RolesAreCompatible(IdentityPrincipal principal)
    {
        if (principal.Roles.Count == 0)
        {
            return false;
        }

        return principal.Roles.All(role => RoleIsCompatible(principal.PrincipalType, role));
    }

    private static bool RoleIsCompatible(PrincipalType principalType, SecurityRole role) =>
        principalType switch
        {
            PrincipalType.Customer => role == SecurityRole.Investor,
            PrincipalType.Staff => role is
                SecurityRole.SupportL1 or
                SecurityRole.SupportL2 or
                SecurityRole.KycAnalyst or
                SecurityRole.AmlAnalyst or
                SecurityRole.ComplianceOfficer or
                SecurityRole.RiskAnalyst or
                SecurityRole.RiskApprover or
                SecurityRole.ReconciliationOperator or
                SecurityRole.ReconciliationApprover or
                SecurityRole.SecurityAnalyst or
                SecurityRole.PlatformAdmin or
                SecurityRole.Sre or
                SecurityRole.Auditor,
            PrincipalType.Service => role is
                SecurityRole.RiskGovernor or
                SecurityRole.ComplianceGate or
                SecurityRole.ExecutionValidator or
                SecurityRole.BrokerAdapter or
                SecurityRole.NotificationService or
                SecurityRole.ReconciliationWorker,
            PrincipalType.AiAgent => role is
                SecurityRole.AiCoach or
                SecurityRole.ResearchAgent or
                SecurityRole.PortfolioAgent or
                SecurityRole.RebalanceAgent,
            _ => false,
        };

    private static bool DeviceRequirementSatisfied(
        DeviceRequirement requirement,
        DeviceTrustState state) =>
        requirement switch
        {
            DeviceRequirement.None => true,
            DeviceRequirement.NotRestricted => state is DeviceTrustState.Trusted or DeviceTrustState.Untrusted,
            DeviceRequirement.Trusted => state == DeviceTrustState.Trusted,
            _ => false,
        };

    private static Dictionary<IdentityAuthorizationAction, AuthorizationRule> BuildRules() => new()
    {
        [IdentityAuthorizationAction.CustomerProfileRead] = Rule(
            Grant(PrincipalType.Customer, SecurityRole.Investor, requiresCustomerOwnership: true)),
        [IdentityAuthorizationAction.PortfolioRead] = Rule(
            Grant(PrincipalType.Customer, SecurityRole.Investor, requiresCustomerOwnership: true)),
        [IdentityAuthorizationAction.ManualOrderCreate] = FinancialCustomerRule(
            Grant(PrincipalType.Customer, SecurityRole.Investor, requiresCustomerOwnership: true)),
        [IdentityAuthorizationAction.AiRecommendationApprove] = FinancialCustomerRule(
            Grant(PrincipalType.Customer, SecurityRole.Investor, requiresCustomerOwnership: true)),
        [IdentityAuthorizationAction.WithdrawalRequest] = FinancialCustomerRule(
            Grant(PrincipalType.Customer, SecurityRole.Investor, requiresCustomerOwnership: true),
            DeviceRequirement.Trusted,
            AuthenticationAssurance.MultiFactor),
        [IdentityAuthorizationAction.BeneficiaryManage] = FinancialCustomerRule(
            Grant(PrincipalType.Customer, SecurityRole.Investor, requiresCustomerOwnership: true),
            DeviceRequirement.Trusted,
            AuthenticationAssurance.MultiFactor),
        [IdentityAuthorizationAction.SessionDeviceRevoke] = Rule(
            grants:
            [
                Grant(PrincipalType.Customer, SecurityRole.Investor, requiresCustomerOwnership: true),
                Grant(PrincipalType.Staff, SecurityRole.SecurityAnalyst, requiredScope: "security.sessions.revoke"),
            ],
            deviceRequirement: DeviceRequirement.NotRestricted,
            requiredStepUpAssurance: AuthenticationAssurance.MultiFactor),
        [IdentityAuthorizationAction.RiskPolicyPropose] = Rule(
            Grant(PrincipalType.Staff, SecurityRole.RiskAnalyst, requiredScope: "risk.policy.propose")),
        [IdentityAuthorizationAction.RiskPolicyApprove] = Rule(
            Grant(PrincipalType.Staff, SecurityRole.RiskApprover, requiredScope: "risk.policy.approve")),
        [IdentityAuthorizationAction.FinancialCorrectionPropose] = Rule(
            Grant(PrincipalType.Staff, SecurityRole.ReconciliationOperator, requiredScope: "reconciliation.correction.propose")),
        [IdentityAuthorizationAction.FinancialCorrectionApprove] = Rule(
            Grant(PrincipalType.Staff, SecurityRole.ReconciliationApprover, requiredScope: "reconciliation.correction.approve")),
        [IdentityAuthorizationAction.DeployApprovedArtifact] = Rule(
            grants: [Grant(PrincipalType.Staff, SecurityRole.Sre, requiredScope: "platform.deploy.approved")],
            deviceRequirement: DeviceRequirement.Trusted,
            requiredStepUpAssurance: AuthenticationAssurance.PhishingResistant),
        [IdentityAuthorizationAction.PortfolioProposalCreate] = Rule(
            Grant(PrincipalType.AiAgent, SecurityRole.PortfolioAgent, requiredScope: "portfolio.proposal.create")),
        [IdentityAuthorizationAction.RebalanceProposalCreate] = Rule(
            Grant(PrincipalType.AiAgent, SecurityRole.RebalanceAgent, requiredScope: "rebalance.proposal.create")),
        [IdentityAuthorizationAction.RiskDecisionEvaluate] = Rule(
            Grant(PrincipalType.Service, SecurityRole.RiskGovernor, requiredScope: "risk.evaluate")),
        [IdentityAuthorizationAction.ComplianceDecisionEvaluate] = Rule(
            Grant(PrincipalType.Service, SecurityRole.ComplianceGate, requiredScope: "compliance.evaluate")),
        [IdentityAuthorizationAction.AuthorizedBrokerOrderSubmit] = Rule(
            Grant(PrincipalType.Service, SecurityRole.ExecutionValidator, requiredScope: "broker.authorized-order.submit")),
        [IdentityAuthorizationAction.BrokerOperationExecute] = Rule(
            Grant(PrincipalType.Service, SecurityRole.BrokerAdapter, requiredScope: "broker.operation.execute")),
        [IdentityAuthorizationAction.SanitizedNotificationSend] = Rule(
            Grant(PrincipalType.Service, SecurityRole.NotificationService, requiredScope: "notifications.sanitized.send")),
    };

    private static AuthorizationRule FinancialCustomerRule(
        AuthorizationGrant grant,
        DeviceRequirement deviceRequirement = DeviceRequirement.NotRestricted,
        AuthenticationAssurance requiredStepUpAssurance = AuthenticationAssurance.Unknown) =>
        Rule(
            grants: [grant],
            requiresNormalAccount: true,
            requiresNormalRecovery: true,
            deviceRequirement: deviceRequirement,
            requiredStepUpAssurance: requiredStepUpAssurance);

    private static AuthorizationRule Rule(
        AuthorizationGrant grant) =>
        Rule([grant]);

    private static AuthorizationRule Rule(
        AuthorizationGrant[] grants,
        bool requiresNormalAccount = false,
        bool requiresNormalRecovery = false,
        DeviceRequirement deviceRequirement = DeviceRequirement.None,
        AuthenticationAssurance requiredStepUpAssurance = AuthenticationAssurance.Unknown) =>
        new(
            grants,
            requiresNormalAccount,
            requiresNormalRecovery,
            deviceRequirement,
            requiredStepUpAssurance);

    private static AuthorizationGrant Grant(
        PrincipalType principalType,
        SecurityRole role,
        bool requiresCustomerOwnership = false,
        string? requiredScope = null) =>
        new(principalType, role, requiresCustomerOwnership, requiredScope);

    private sealed record AuthorizationGrant(
        PrincipalType PrincipalType,
        SecurityRole Role,
        bool RequiresCustomerOwnership,
        string? RequiredScope);

    private sealed record AuthorizationRule(
        AuthorizationGrant[] Grants,
        bool RequiresNormalAccount,
        bool RequiresNormalRecovery,
        DeviceRequirement DeviceRequirement,
        AuthenticationAssurance RequiredStepUpAssurance);

    private enum DeviceRequirement
    {
        None = 0,
        NotRestricted = 1,
        Trusted = 2,
    }
}
