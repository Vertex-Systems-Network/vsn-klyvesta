namespace Klyvesta.Domain.Compliance;

public enum ComplianceDecisionOutcome
{
    Allow,
    Deny,
    Hold,
}

public enum ComplianceAccountStatus
{
    Unknown,
    Eligible,
    PendingReview,
    Restricted,
    Suspended,
}

public enum RegulatoryFeatureStatus
{
    Unknown,
    Enabled,
    Pending,
    Disabled,
}

public enum ManualReviewStatus
{
    NotRequired,
    Approved,
    Pending,
    Rejected,
}

public enum MandateStatus
{
    Unknown,
    Active,
    Pending,
    Expired,
    Revoked,
}

public enum InstrumentRestrictionStatus
{
    Unknown,
    Allowed,
    Restricted,
    ManualReview,
}

public enum PaperExecutionMode
{
    Manual,
    AutoSimulation,
}

public sealed record PaperCompliancePolicy(
    string Version,
    bool PaperOrderSubmissionEnabled,
    bool RequireMandateForAutoSimulation);

public sealed record ComplianceMandateEvidence(
    string MandateReference,
    MandateStatus Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveUntil);

public sealed record ComplianceEvaluationRequest(
    string AccountReference,
    string InstrumentReference,
    PaperExecutionMode ExecutionMode,
    ComplianceAccountStatus AccountStatus,
    RegulatoryFeatureStatus RegulatoryFeatureStatus,
    ManualReviewStatus ManualReviewStatus,
    InstrumentRestrictionStatus InstrumentRestrictionStatus,
    ComplianceMandateEvidence? Mandate,
    DateTimeOffset EvaluatedAt);

public sealed record ComplianceDecision(
    ComplianceDecisionOutcome Outcome,
    string PolicyVersion,
    string PrimaryReasonCode,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset EvaluatedAt,
    string AccountReference,
    string InstrumentReference,
    PaperExecutionMode ExecutionMode);
