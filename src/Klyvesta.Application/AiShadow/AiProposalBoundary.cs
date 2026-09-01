using System.Text.Json;
using System.Text.Json.Serialization;
using Klyvesta.Domain.AiShadow;

namespace Klyvesta.Application.AiShadow;

public sealed record AiProposalParseResult(
    AiInvestmentProposal? Proposal,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Proposal is not null && Errors.Count == 0;
}

public sealed record AiProposalValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed class AiProposalJsonParser
{
    private const int MaxPayloadLength = 65_536;

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public AiProposalParseResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Invalid("AI_PROPOSAL_JSON_EMPTY");
        }

        if (json.Length > MaxPayloadLength)
        {
            return Invalid("AI_PROPOSAL_JSON_TOO_LARGE");
        }

        try
        {
            var dto = JsonSerializer.Deserialize<ProposalDto>(json, JsonOptions);
            if (dto is null)
            {
                return Invalid("AI_PROPOSAL_JSON_NULL");
            }

            var actions = dto.Actions?.Select(action => new AiProposalAction(
                action.InstrumentReference ?? string.Empty,
                action.Action,
                action.TargetWeight)).ToArray() ?? [];

            return new AiProposalParseResult(
                new AiInvestmentProposal(
                    dto.ProposalId,
                    dto.PortfolioContextReference ?? string.Empty,
                    dto.CustomerContextReference ?? string.Empty,
                    actions,
                    dto.EvidenceReferences?.ToArray() ?? [],
                    dto.Confidence,
                    dto.Uncertainty,
                    dto.ModelVersion ?? string.Empty,
                    dto.PromptVersion ?? string.Empty,
                    dto.GeneratedAt,
                    dto.DataObservedAt,
                    dto.Explanation ?? string.Empty),
                []);
        }
        catch (JsonException)
        {
            return Invalid("AI_PROPOSAL_JSON_SCHEMA_REJECTED");
        }
        catch (NotSupportedException)
        {
            return Invalid("AI_PROPOSAL_JSON_SCHEMA_REJECTED");
        }
    }

    private static AiProposalParseResult Invalid(string error) =>
        new(null, [error]);

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 16,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private sealed class ProposalDto
    {
        public Guid ProposalId { get; set; }

        public string? PortfolioContextReference { get; set; }

        public string? CustomerContextReference { get; set; }

        public List<ActionDto>? Actions { get; set; }

        public List<string>? EvidenceReferences { get; set; }

        public decimal Confidence { get; set; }

        public decimal Uncertainty { get; set; }

        public string? ModelVersion { get; set; }

        public string? PromptVersion { get; set; }

        public DateTimeOffset GeneratedAt { get; set; }

        public DateTimeOffset DataObservedAt { get; set; }

        public string? Explanation { get; set; }
    }

    private sealed class ActionDto
    {
        public string? InstrumentReference { get; set; }

        public AiProposalActionKind Action { get; set; }

        public decimal? TargetWeight { get; set; }
    }
}

public sealed class DeterministicAiProposalValidator
{
    public AiProposalValidationResult Validate(
        AiInvestmentProposal proposal,
        AiProposalValidationPolicy policy,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(policy);
        ValidatePolicy(policy);

        var errors = new List<string>();

        if (proposal.ProposalId == Guid.Empty)
        {
            errors.Add("AI_PROPOSAL_ID_MISSING");
        }

        if (string.IsNullOrWhiteSpace(proposal.PortfolioContextReference))
        {
            errors.Add("AI_PORTFOLIO_CONTEXT_REFERENCE_MISSING");
        }

        if (string.IsNullOrWhiteSpace(proposal.CustomerContextReference))
        {
            errors.Add("AI_CUSTOMER_CONTEXT_REFERENCE_MISSING");
        }

        if (string.IsNullOrWhiteSpace(proposal.ModelVersion))
        {
            errors.Add("AI_MODEL_VERSION_MISSING");
        }

        if (string.IsNullOrWhiteSpace(proposal.PromptVersion))
        {
            errors.Add("AI_PROMPT_VERSION_MISSING");
        }

        if (proposal.Explanation.Length > policy.MaxExplanationLength)
        {
            errors.Add("AI_EXPLANATION_TOO_LARGE");
        }

        if (proposal.Confidence is < 0m or > 1m)
        {
            errors.Add("AI_CONFIDENCE_OUT_OF_RANGE");
        }

        if (proposal.Uncertainty is < 0m or > 1m)
        {
            errors.Add("AI_UNCERTAINTY_OUT_OF_RANGE");
        }

        if (proposal.GeneratedAt > evaluatedAt + policy.MaxFutureSkew)
        {
            errors.Add("AI_PROPOSAL_GENERATED_IN_FUTURE");
        }
        else if (evaluatedAt - proposal.GeneratedAt > policy.MaxProposalAge)
        {
            errors.Add("AI_PROPOSAL_STALE");
        }

        if (proposal.DataObservedAt > proposal.GeneratedAt + policy.MaxFutureSkew ||
            proposal.DataObservedAt > evaluatedAt + policy.MaxFutureSkew)
        {
            errors.Add("AI_DATA_TIMESTAMP_INVALID");
        }
        else if (evaluatedAt - proposal.DataObservedAt > policy.MaxDataAge)
        {
            errors.Add("AI_DATA_STALE");
        }

        ValidateEvidenceReferences(proposal.EvidenceReferences, policy, errors);
        ValidateActions(proposal.Actions, policy, errors);

        return new AiProposalValidationResult(errors.Count == 0, errors.ToArray());
    }

    private static void ValidateEvidenceReferences(
        IReadOnlyList<string> evidenceReferences,
        AiProposalValidationPolicy policy,
        List<string> errors)
    {
        if (evidenceReferences.Count == 0)
        {
            errors.Add("AI_EVIDENCE_REFERENCES_MISSING");
            return;
        }

        if (evidenceReferences.Count > policy.MaxEvidenceReferences)
        {
            errors.Add("AI_EVIDENCE_REFERENCE_LIMIT_EXCEEDED");
        }

        if (evidenceReferences.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("AI_EVIDENCE_REFERENCE_INVALID");
        }

        if (evidenceReferences.Distinct(StringComparer.Ordinal).Count() != evidenceReferences.Count)
        {
            errors.Add("AI_EVIDENCE_REFERENCE_DUPLICATE");
        }
    }

    private static void ValidateActions(
        IReadOnlyList<AiProposalAction> actions,
        AiProposalValidationPolicy policy,
        List<string> errors)
    {
        if (actions.Count == 0)
        {
            errors.Add("AI_ACTIONS_MISSING");
            return;
        }

        if (actions.Count > policy.MaxActions)
        {
            errors.Add("AI_ACTION_LIMIT_EXCEEDED");
        }

        if (actions.Any(action => string.IsNullOrWhiteSpace(action.InstrumentReference)))
        {
            errors.Add("AI_ACTION_INSTRUMENT_INVALID");
        }

        if (actions.GroupBy(action => action.InstrumentReference, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            errors.Add("AI_ACTION_INSTRUMENT_DUPLICATE");
        }

        decimal targetWeightTotal = 0m;
        foreach (var action in actions)
        {
            switch (action.Action)
            {
                case AiProposalActionKind.TargetAllocation:
                    if (action.TargetWeight is null ||
                        action.TargetWeight < 0m ||
                        action.TargetWeight > policy.MaxTargetWeight)
                    {
                        errors.Add("AI_TARGET_WEIGHT_INVALID");
                    }
                    else
                    {
                        targetWeightTotal += action.TargetWeight.Value;
                    }

                    break;
                case AiProposalActionKind.Hold:
                    if (action.TargetWeight is not null)
                    {
                        errors.Add("AI_HOLD_TARGET_WEIGHT_FORBIDDEN");
                    }

                    break;
                default:
                    errors.Add("AI_ACTION_KIND_UNKNOWN");
                    break;
            }
        }

        if (targetWeightTotal > 1m)
        {
            errors.Add("AI_TARGET_WEIGHT_TOTAL_EXCEEDED");
        }
    }

    private static void ValidatePolicy(AiProposalValidationPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Version) ||
            policy.MaxProposalAge <= TimeSpan.Zero ||
            policy.MaxDataAge <= TimeSpan.Zero ||
            policy.MaxFutureSkew < TimeSpan.Zero ||
            policy.MaxActions <= 0 ||
            policy.MaxEvidenceReferences <= 0 ||
            policy.MaxExplanationLength <= 0 ||
            policy.MaxTargetWeight is <= 0m or > 1m)
        {
            throw new ArgumentException("AI proposal validation policy is invalid or incomplete.", nameof(policy));
        }
    }
}
