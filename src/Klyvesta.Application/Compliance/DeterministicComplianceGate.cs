using Klyvesta.Domain.Compliance;

namespace Klyvesta.Application.Compliance;

public interface IComplianceGate
{
    ComplianceDecision Evaluate(PaperCompliancePolicy policy, ComplianceEvaluationRequest request);
}

public sealed class DeterministicComplianceGate : IComplianceGate
{
    public ComplianceDecision Evaluate(PaperCompliancePolicy policy, ComplianceEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(request);
        ValidatePolicy(policy);

        var denials = new List<string>();
        var holds = new List<string>();

        if (string.IsNullOrWhiteSpace(request.AccountReference) ||
            string.IsNullOrWhiteSpace(request.InstrumentReference))
        {
            denials.Add("COMPLIANCE_INVALID_ORDER_CONTEXT");
        }

        if (!policy.PaperOrderSubmissionEnabled)
        {
            denials.Add("COMPLIANCE_POLICY_PAPER_ORDER_DISABLED");
        }

        EvaluateAccountStatus(request.AccountStatus, denials, holds);
        EvaluateRegulatoryFeature(request.RegulatoryFeatureStatus, denials, holds);
        EvaluateManualReview(request.ManualReviewStatus, denials, holds);
        EvaluateInstrumentRestriction(request.InstrumentRestrictionStatus, denials, holds);

        if (request.ExecutionMode == PaperExecutionMode.AutoSimulation && policy.RequireMandateForAutoSimulation)
        {
            EvaluateMandate(request.Mandate, request.EvaluatedAt, denials, holds);
        }

        if (denials.Count > 0)
        {
            var reasons = denials.Concat(holds).ToArray();
            return CreateDecision(ComplianceDecisionOutcome.Deny, policy.Version, reasons, request);
        }

        if (holds.Count > 0)
        {
            return CreateDecision(ComplianceDecisionOutcome.Hold, policy.Version, holds, request);
        }

        return CreateDecision(
            ComplianceDecisionOutcome.Allow,
            policy.Version,
            ["COMPLIANCE_ALLOW"],
            request);
    }

    private static void ValidatePolicy(PaperCompliancePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Version))
        {
            throw new ArgumentException("Compliance policy version is required.", nameof(policy));
        }
    }

    private static void EvaluateAccountStatus(
        ComplianceAccountStatus status,
        List<string> denials,
        List<string> holds)
    {
        switch (status)
        {
            case ComplianceAccountStatus.Eligible:
                return;
            case ComplianceAccountStatus.Restricted:
                denials.Add("COMPLIANCE_ACCOUNT_RESTRICTED");
                return;
            case ComplianceAccountStatus.Suspended:
                denials.Add("COMPLIANCE_ACCOUNT_SUSPENDED");
                return;
            case ComplianceAccountStatus.PendingReview:
                holds.Add("COMPLIANCE_ACCOUNT_PENDING_REVIEW");
                return;
            case ComplianceAccountStatus.Unknown:
            default:
                holds.Add("COMPLIANCE_ACCOUNT_STATUS_UNKNOWN");
                return;
        }
    }

    private static void EvaluateRegulatoryFeature(
        RegulatoryFeatureStatus status,
        List<string> denials,
        List<string> holds)
    {
        switch (status)
        {
            case RegulatoryFeatureStatus.Enabled:
                return;
            case RegulatoryFeatureStatus.Disabled:
                denials.Add("COMPLIANCE_REGULATORY_FEATURE_DISABLED");
                return;
            case RegulatoryFeatureStatus.Pending:
                holds.Add("COMPLIANCE_REGULATORY_FEATURE_PENDING");
                return;
            case RegulatoryFeatureStatus.Unknown:
            default:
                holds.Add("COMPLIANCE_REGULATORY_FEATURE_UNKNOWN");
                return;
        }
    }

    private static void EvaluateManualReview(
        ManualReviewStatus status,
        List<string> denials,
        List<string> holds)
    {
        switch (status)
        {
            case ManualReviewStatus.NotRequired:
            case ManualReviewStatus.Approved:
                return;
            case ManualReviewStatus.Rejected:
                denials.Add("COMPLIANCE_MANUAL_REVIEW_REJECTED");
                return;
            case ManualReviewStatus.Pending:
            default:
                holds.Add("COMPLIANCE_MANUAL_REVIEW_PENDING");
                return;
        }
    }

    private static void EvaluateInstrumentRestriction(
        InstrumentRestrictionStatus status,
        List<string> denials,
        List<string> holds)
    {
        switch (status)
        {
            case InstrumentRestrictionStatus.Allowed:
                return;
            case InstrumentRestrictionStatus.Restricted:
                denials.Add("COMPLIANCE_INSTRUMENT_RESTRICTED");
                return;
            case InstrumentRestrictionStatus.ManualReview:
                holds.Add("COMPLIANCE_INSTRUMENT_MANUAL_REVIEW");
                return;
            case InstrumentRestrictionStatus.Unknown:
            default:
                holds.Add("COMPLIANCE_INSTRUMENT_RESTRICTION_UNKNOWN");
                return;
        }
    }

    private static void EvaluateMandate(
        ComplianceMandateEvidence? mandate,
        DateTimeOffset evaluatedAt,
        List<string> denials,
        List<string> holds)
    {
        if (mandate is null)
        {
            holds.Add("COMPLIANCE_MANDATE_MISSING");
            return;
        }

        switch (mandate.Status)
        {
            case MandateStatus.Revoked:
                denials.Add("COMPLIANCE_MANDATE_REVOKED");
                return;
            case MandateStatus.Expired:
                denials.Add("COMPLIANCE_MANDATE_EXPIRED");
                return;
            case MandateStatus.Pending:
                holds.Add("COMPLIANCE_MANDATE_PENDING");
                return;
            case MandateStatus.Unknown:
                holds.Add("COMPLIANCE_MANDATE_STATUS_UNKNOWN");
                return;
            case MandateStatus.Active:
                EvaluateActiveMandateWindow(mandate, evaluatedAt, denials, holds);
                return;
            default:
                holds.Add("COMPLIANCE_MANDATE_STATUS_UNKNOWN");
                return;
        }
    }

    private static void EvaluateActiveMandateWindow(
        ComplianceMandateEvidence mandate,
        DateTimeOffset evaluatedAt,
        List<string> denials,
        List<string> holds)
    {
        if (string.IsNullOrWhiteSpace(mandate.MandateReference) ||
            mandate.EffectiveFrom is null ||
            mandate.EffectiveUntil is null)
        {
            holds.Add("COMPLIANCE_MANDATE_EVIDENCE_INCOMPLETE");
            return;
        }

        if (mandate.EffectiveUntil < mandate.EffectiveFrom)
        {
            holds.Add("COMPLIANCE_MANDATE_WINDOW_INVALID");
            return;
        }

        if (evaluatedAt < mandate.EffectiveFrom)
        {
            holds.Add("COMPLIANCE_MANDATE_NOT_YET_EFFECTIVE");
            return;
        }

        if (evaluatedAt > mandate.EffectiveUntil)
        {
            denials.Add("COMPLIANCE_MANDATE_EXPIRED");
        }
    }

    private static ComplianceDecision CreateDecision(
        ComplianceDecisionOutcome outcome,
        string policyVersion,
        IReadOnlyList<string> reasons,
        ComplianceEvaluationRequest request) =>
        new(
            outcome,
            policyVersion,
            reasons[0],
            reasons,
            request.EvaluatedAt,
            request.AccountReference,
            request.InstrumentReference,
            request.ExecutionMode);
}
