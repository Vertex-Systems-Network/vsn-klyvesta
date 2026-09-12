using Klyvesta.Domain.Common;
using Klyvesta.Domain.Persistence;
using Klyvesta.Domain.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klyvesta.Domain.Services;

/// <summary>
/// Service implementation for Risk Governor policy evaluation and decision logging.
/// Enforces deterministic risk checks with complete audit trails.
/// AI cannot override DENY decisions.
/// </summary>
public class RiskGovernorService : IRiskGovernorService
{
    private readonly KlyvestaDbContext _dbContext;
    private readonly ILogger<RiskGovernorService> _logger;

    public RiskGovernorService(
        KlyvestaDbContext dbContext,
        ILogger<RiskGovernorService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<RiskDecision> EvaluateOrderRiskAsync(
        CustomerId customerId,
        AccountId accountId,
        Symbol symbol,
        Side side,
        Quantity quantity,
        Price price,
        AiProposalId? aiProposalId,
        CancellationToken cancellationToken = default)
    {
        // Get active risk policies
        var policies = await _dbContext.RiskPolicies
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Version)
            .ToListAsync(cancellationToken);

        var orderValue = quantity * price;
        var decisions = new List<(RiskPolicyEntity Policy, bool Passed, string Reason)>();

        foreach (var policy in policies)
        {
            var (passed, reason) = EvaluatePolicy(policy, customerId, accountId, symbol, side, quantity, price, orderValue);
            decisions.Add((policy, passed, reason));

            if (!passed && policy.EnforcementLevel == RiskEnforcementLevel.Deny)
            {
                // Immediate denial - no further checks needed
                var decision = RiskDecision.Deny(
                    aiProposalId,
                    $"Risk policy violation: {reason}",
                    policy.Id,
                    policy.Version);

                await LogDecisionAsync(decision, cancellationToken);
                return decision;
            }
        }

        // All checks passed or warnings only
        var warningReasons = decisions.Where(d => !d.Passed).Select(d => d.Reason).ToList();
        var decisionResult = warningReasons.Count > 0
            ? RiskDecision.PassWithWarnings(aiProposalId, warningReasons)
            : RiskDecision.Pass(aiProposalId);

        // Attach highest version policy ID for audit
        if (policies.Count > 0)
        {
            decisionResult = decisionResult with { PolicyId = policies[0].Id, PolicyVersion = policies[0].Version };
        }

        await LogDecisionAsync(decisionResult, cancellationToken);
        return decisionResult;
    }

    public async Task<RiskDecision> EvaluatePortfolioConcentrationAsync(
        AccountId accountId,
        Symbol symbol,
        Quantity additionalQuantity,
        Price currentPrice,
        AiProposalId? aiProposalId,
        CancellationToken cancellationToken = default)
    {
        // Get current portfolio positions (simplified - would need position service integration)
        var positions = await _dbContext.OrderExecutions
            .Where(e => e.OrderIntent.AccountId == accountId && e.SettlementDateUtc <= DateTime.UtcNow)
            .GroupBy(e => e.OrderIntent.Symbol)
            .Select(g => new
            {
                Symbol = g.Key,
                TotalQuantity = g.Sum(e => e.FillQuantity),
                AveragePrice = g.Average(e => e.FillPrice)
            })
            .ToListAsync(cancellationToken);

        var totalPortfolioValue = positions.Sum(p => p.TotalQuantity * p.AveragePrice);
        var newPositionValue = additionalQuantity.Units * currentPrice.Units;
        var updatedPositionValue = positions
            .Where(p => p.Symbol == symbol.Value)
            .Sum(p => p.TotalQuantity * p.AveragePrice) + newPositionValue;

        var concentrationRatio = totalPortfolioValue > 0 
            ? updatedPositionValue / (totalPortfolioValue + newPositionValue) 
            : 1.0m;

        // Get concentration limit policy
        var concentrationPolicy = await _dbContext.RiskPolicies
            .FirstOrDefaultAsync(p => p.PolicyType == RiskPolicyType.ConcentrationLimit && p.IsActive, cancellationToken);

        if (concentrationPolicy != null && concentrationRatio > concentrationPolicy.ThresholdBasisPoints / 10000.0m)
        {
            var decision = RiskDecision.Deny(
                aiProposalId,
                $"Concentration ratio {concentrationRatio:P2} exceeds limit {concentrationPolicy.ThresholdBasisPoints / 10000.0m:P2}",
                concentrationPolicy.Id,
                concentrationPolicy.Version);

            await LogDecisionAsync(decision, cancellationToken);
            return decision;
        }

        return RiskDecision.Pass(aiProposalId);
    }

    public async Task<RiskDecision> EvaluateDailyLossLimitAsync(
        AccountId accountId,
        decimal currentDayPnL,
        AiProposalId? aiProposalId,
        CancellationToken cancellationToken = default)
    {
        // Get daily loss limit policy
        var lossLimitPolicy = await _dbContext.RiskPolicies
            .FirstOrDefaultAsync(p => p.PolicyType == RiskPolicyType.DailyLossLimit && p.IsActive, cancellationToken);

        if (lossLimitPolicy != null)
        {
            var maxLoss = lossLimitPolicy.ThresholdBasisPoints / 10000.0m; // Convert basis points to decimal
            
            if (currentDayPnL < -maxLoss)
            {
                var decision = RiskDecision.Deny(
                    aiProposalId,
                    $"Daily loss {currentDayPnL:C} exceeds limit {-maxLoss:C}",
                    lossLimitPolicy.Id,
                    lossLimitPolicy.Version);

                await LogDecisionAsync(decision, cancellationToken);
                return decision;
            }
        }

        return RiskDecision.Pass(aiProposalId);
    }

    public async Task<RiskPolicy> CreatePolicyAsync(
        RiskPolicyType policyType,
        string description,
        int thresholdBasisPoints,
        RiskEnforcementLevel enforcementLevel,
        string? scopeCustomerId = null,
        string? scopeAccountId = null,
        string? scopeSymbol = null,
        CancellationToken cancellationToken = default)
    {
        var policy = new RiskPolicyEntity
        {
            Id = Guid.NewGuid(),
            PolicyType = policyType,
            Description = description,
            ThresholdBasisPoints = thresholdBasisPoints,
            EnforcementLevel = enforcementLevel,
            ScopeCustomerId = scopeCustomerId != null ? new CustomerId(Guid.Parse(scopeCustomerId)) : null,
            ScopeAccountId = scopeAccountId != null ? new AccountId(Guid.Parse(scopeAccountId)) : null,
            ScopeSymbol = scopeSymbol,
            IsActive = true,
            Version = 1,
            ExternalTimestampUtc = DateTime.UtcNow,
            ObservedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.RiskPolicies.AddAsync(policy, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created risk policy {PolicyId} type {PolicyType}", policy.Id, policyType);

        return MapToDomain(policy);
    }

    public async Task<RiskPolicy> UpdatePolicyAsync(
        RiskPolicyId policyId,
        string description,
        int thresholdBasisPoints,
        RiskEnforcementLevel enforcementLevel,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RiskPolicies
            .FirstOrDefaultAsync(p => p.Id == policyId, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"Risk policy {policyId} not found");
        }

        // Increment version
        entity.Description = description;
        entity.ThresholdBasisPoints = thresholdBasisPoints;
        entity.EnforcementLevel = enforcementLevel;
        entity.Version++;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated risk policy {PolicyId} to version {Version}", policyId, entity.Version);

        return MapToDomain(entity);
    }

    public async Task<IReadOnlyList<RiskDecision>> GetDecisionsByAccountAsync(
        AccountId accountId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RiskDecisions
            .Where(d => d.AccountId == accountId);

        if (fromUtc.HasValue)
        {
            query = query.Where(d => d.ObservedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(d => d.ObservedAtUtc <= toUtc.Value);
        }

        var entities = await query
            .OrderByDescending(d => d.ObservedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    private (bool Passed, string Reason) EvaluatePolicy(
        RiskPolicyEntity policy,
        CustomerId customerId,
        AccountId accountId,
        Symbol symbol,
        Side side,
        Quantity quantity,
        Price price,
        Money orderValue)
    {
        // Check scope filters
        if (policy.ScopeCustomerId != null && policy.ScopeCustomerId != customerId)
            return (true, "Out of scope (customer)");

        if (policy.ScopeAccountId != null && policy.ScopeAccountId != accountId)
            return (true, "Out of scope (account)");

        if (policy.ScopeSymbol != null && policy.ScopeSymbol != symbol.Value)
            return (true, "Out of scope (symbol)");

        // Evaluate based on policy type
        switch (policy.PolicyType)
        {
            case RiskPolicyType.OrderValueLimit:
                var maxValue = policy.ThresholdBasisPoints / 10000.0m * 1000000m; // Simplified
                if (orderValue.Units > maxValue)
                    return (false, $"Order value {orderValue} exceeds limit {new Money(maxValue, orderValue.Currency)}");
                break;

            case RiskPolicyType.PositionSizeLimit:
                var maxQuantity = policy.ThresholdBasisPoints; // Interpret as absolute units
                if (quantity.Units > maxQuantity)
                    return (false, $"Quantity {quantity} exceeds limit {maxQuantity}");
                break;

            case RiskPolicyType.ConcentrationLimit:
            case RiskPolicyType.DailyLossLimit:
                // Handled by specialized methods
                return (true, "Requires portfolio context");
        }

        return (true, "Passed");
    }

    private async Task LogDecisionAsync(RiskDecision decision, CancellationToken cancellationToken)
    {
        var entity = new RiskDecisionEntity
        {
            Id = decision.Id,
            AccountId = decision.AccountId ?? default,
            AiProposalId = decision.AiProposalId,
            DecisionType = decision.DecisionType,
            DecisionOutcome = decision.Outcome,
            Reason = decision.Reason,
            PolicyId = decision.PolicyId,
            PolicyVersion = decision.PolicyVersion,
            RiskLevel = decision.RiskLevel,
            ExternalTimestampUtc = decision.ExternalTimestampUtc,
            ObservedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.RiskDecisions.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Logged risk decision {DecisionId}: {Outcome} - {Reason}", 
            decision.Id, decision.Outcome, decision.Reason);
    }

    private RiskPolicy MapToDomain(RiskPolicyEntity entity)
    {
        return new RiskPolicy(
            entity.Id,
            entity.PolicyType,
            entity.Description,
            entity.ThresholdBasisPoints,
            entity.EnforcementLevel,
            entity.ScopeCustomerId,
            entity.ScopeAccountId,
            entity.ScopeSymbol,
            entity.IsActive,
            entity.Version)
        {
            ExternalTimestampUtc = entity.ExternalTimestampUtc,
            ObservedAtUtc = entity.ObservedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private RiskDecision MapToDomain(RiskDecisionEntity entity)
    {
        return new RiskDecision(
            entity.Id,
            entity.AccountId,
            entity.AiProposalId,
            entity.DecisionType,
            entity.DecisionOutcome,
            entity.Reason,
            entity.PolicyId,
            entity.PolicyVersion,
            entity.RiskLevel)
        {
            ExternalTimestampUtc = entity.ExternalTimestampUtc,
            ObservedAtUtc = entity.ObservedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }
}
