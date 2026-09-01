using System.Security.Cryptography;
using System.Text;
using Klyvesta.Application.Brokerage;
using Klyvesta.Domain.AiShadow;
using Klyvesta.Domain.Compliance;
using Klyvesta.Domain.Risk;
using Klyvesta.Infrastructure.Brokerage.Paper;

namespace Klyvesta.Infrastructure.AiShadow;

public sealed record PaperShadowExecutionResult(
    int ItemIndex,
    bool Submitted,
    string ReasonCode,
    BrokerOperationResult<BrokerOrderSnapshot>? BrokerResult);

public sealed class PaperShadowOrderExecutor
{
    private readonly PaperBrokerAdapter _paperBroker;

    public PaperShadowOrderExecutor(PaperBrokerAdapter paperBroker)
    {
        _paperBroker = paperBroker ?? throw new ArgumentNullException(nameof(paperBroker));
    }

    public async Task<PaperShadowExecutionResult> ExecuteItemAsync(
        AiShadowPlan plan,
        int itemIndex,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (itemIndex < 0 || itemIndex >= plan.Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(itemIndex));
        }

        var item = plan.Items[itemIndex];
        var readinessFailure = ValidateReadyItem(plan, item);
        if (readinessFailure is not null)
        {
            return new PaperShadowExecutionResult(itemIndex, false, readinessFailure, null);
        }

        var brokerOrderId = DeterministicGuid(plan.ProposalId, itemIndex, "broker-order");
        var orderIntentId = DeterministicGuid(plan.ProposalId, itemIndex, "order-intent");
        var command = new SubmitBrokerOrderCommand(
            brokerOrderId,
            orderIntentId,
            $"ai-shadow-{plan.ProposalId:N}-{itemIndex:D3}",
            plan.AccountReference,
            item.InstrumentReference,
            item.Side == RiskTradeSide.Buy ? BrokerOrderSide.Buy : BrokerOrderSide.Sell,
            BrokerOrderType.Limit,
            item.Quantity,
            item.AuthoritativePrice,
            BrokerTimeInForce.Day);

        var result = await _paperBroker.SubmitOrderAsync(command, cancellationToken).ConfigureAwait(false);
        return new PaperShadowExecutionResult(
            itemIndex,
            result.State == BrokerResultState.Success,
            result.State == BrokerResultState.Success ? "AI_SHADOW_PAPER_SUBMITTED" : result.ErrorCode ?? "AI_SHADOW_PAPER_SUBMIT_FAILED",
            result);
    }

    public async Task<IReadOnlyList<PaperShadowExecutionResult>> ExecuteReadyAsync(
        AiShadowPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var results = new List<PaperShadowExecutionResult>();
        for (var index = 0; index < plan.Items.Count; index++)
        {
            if (plan.Items[index].Status != ShadowPlanItemStatus.ReadyForPaper)
            {
                continue;
            }

            results.Add(await ExecuteItemAsync(plan, index, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    private static string? ValidateReadyItem(AiShadowPlan plan, AiShadowPlanItem item)
    {
        if (item.Status != ShadowPlanItemStatus.ReadyForPaper)
        {
            return "AI_SHADOW_ITEM_NOT_READY";
        }

        if (item.Side is null || item.Quantity <= 0m || item.AuthoritativePrice <= 0m)
        {
            return "AI_SHADOW_ITEM_ECONOMICS_INVALID";
        }

        if (item.RiskDecision?.Outcome != RiskDecisionOutcome.Allow ||
            !StringComparer.Ordinal.Equals(item.RiskDecision.PolicyVersion, plan.RiskPolicyVersion))
        {
            return "AI_SHADOW_RISK_EVIDENCE_NOT_ALLOW";
        }

        if (item.ComplianceDecision?.Outcome != ComplianceDecisionOutcome.Allow ||
            !StringComparer.Ordinal.Equals(item.ComplianceDecision.PolicyVersion, plan.CompliancePolicyVersion))
        {
            return "AI_SHADOW_COMPLIANCE_EVIDENCE_NOT_ALLOW";
        }

        return null;
    }

    private static Guid DeterministicGuid(Guid proposalId, int itemIndex, string purpose)
    {
        var input = Encoding.UTF8.GetBytes($"{proposalId:N}:{itemIndex:D6}:{purpose}");
        var hash = SHA256.HashData(input);
        return new Guid(hash.AsSpan(0, 16));
    }
}
