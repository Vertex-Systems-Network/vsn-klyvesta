using Klyvesta.Domain.Common;
using Klyvesta.Domain.Persistence;
using Klyvesta.Domain.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klyvesta.Domain.Services;

/// <summary>
/// Service implementation for Compliance Gate mandate enforcement and regulatory checks.
/// Enforces customer authorization, trading restrictions, and manual review workflows.
/// AI cannot override DENY decisions.
/// </summary>
public class ComplianceGateService : IComplianceGateService
{
    private readonly KlyvestaDbContext _dbContext;
    private readonly ILogger<ComplianceGateService> _logger;

    public ComplianceGateService(
        KlyvestaDbContext dbContext,
        ILogger<ComplianceGateService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ComplianceDecision> EvaluateOrderComplianceAsync(
        CustomerId customerId,
        AccountId accountId,
        Symbol symbol,
        Side side,
        Quantity quantity,
        Price price,
        DateTime orderTimestamp,
        AiProposalId? aiProposalId,
        CancellationToken cancellationToken = default)
    {
        // Get active compliance policies
        var policies = await _dbContext.CompliancePolicies
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        var decisions = new List<(CompliancePolicyEntity Policy, bool Passed, string Reason)>();

        foreach (var policy in policies)
        {
            var (passed, reason) = await EvaluatePolicyAsync(
                policy, customerId, accountId, symbol, side, quantity, price, orderTimestamp, cancellationToken);
            
            decisions.Add((policy, passed, reason));

            if (!passed && policy.EnforcementLevel == ComplianceEnforcementLevel.Deny)
            {
                var decision = ComplianceDecision.Deny(
                    aiProposalId,
                    $"Compliance policy violation: {reason}",
                    policy.Id,
                    policy.Version);

                await LogDecisionAsync(decision, null, cancellationToken);
                return decision;
            }
        }

        // Check mandate requirement for auto-trading
        var requiresMandate = policies.Any(p => p.RequiresMandate);
        if (requiresMandate && aiProposalId.HasValue)
        {
            var activeMandate = await GetActiveMandateAsync(customerId, cancellationToken);
            if (activeMandate == null)
            {
                var decision = ComplianceDecision.Deny(
                    aiProposalId,
                    "No active trading mandate found for customer");

                await LogDecisionAsync(decision, null, cancellationToken);
                return decision;
            }
        }

        // All checks passed or warnings only
        var warningReasons = decisions.Where(d => !d.Passed).Select(d => d.Reason).ToList();
        var decisionResult = warningReasons.Count > 0
            ? ComplianceDecision.PassWithWarnings(aiProposalId, warningReasons)
            : ComplianceDecision.Pass(aiProposalId);

        await LogDecisionAsync(decisionResult, null, cancellationToken);
        return decisionResult;
    }

    public async Task<Mandate> CreateMandateAsync(
        CustomerId customerId,
        string mandateText,
        string ipAddress,
        string deviceFingerprint,
        CancellationToken cancellationToken = default)
    {
        var mandate = new MandateEntity
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            MandateText = mandateText,
            Status = MandateStatus.PendingAcceptance,
            IpAddress = ipAddress,
            DeviceFingerprint = deviceFingerprint,
            ExternalTimestampUtc = DateTime.UtcNow,
            ObservedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.Mandates.AddAsync(mandate, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created mandate {MandateId} for customer {CustomerId}", mandate.Id, customerId);

        return MapToDomain(mandate);
    }

    public async Task<Mandate> AcceptMandateAsync(
        MandateId mandateId,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Mandates
            .FirstOrDefaultAsync(m => m.Id == mandateId, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"Mandate {mandateId} not found");
        }

        if (entity.Status != MandateStatus.PendingAcceptance)
        {
            throw new InvalidOperationException($"Mandate {mandateId} is not pending acceptance (current: {entity.Status})");
        }

        entity.Status = MandateStatus.Active;
        entity.AcceptedAtUtc = DateTime.UtcNow;
        entity.IpAddress = ipAddress;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Accepted mandate {MandateId} for customer {CustomerId}", mandateId, entity.CustomerId);

        return MapToDomain(entity);
    }

    public async Task<Mandate> RevokeMandateAsync(
        MandateId mandateId,
        string reason,
        string revokedBy,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Mandates
            .FirstOrDefaultAsync(m => m.Id == mandateId, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"Mandate {mandateId} not found");
        }

        if (entity.Status != MandateStatus.Active)
        {
            throw new InvalidOperationException($"Mandate {mandateId} is not active (current: {entity.Status})");
        }

        entity.Status = MandateStatus.Revoked;
        entity.RevokedAtUtc = DateTime.UtcNow;
        entity.RevocationReason = reason;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Revoked mandate {MandateId}: {Reason}", mandateId, reason);

        return MapToDomain(entity);
    }

    public async Task<ComplianceDecision> RequestManualReviewAsync(
        CustomerId customerId,
        AccountId accountId,
        string reviewReason,
        string requestedBy,
        AiProposalId? aiProposalId,
        CancellationToken cancellationToken = default)
    {
        var decision = ComplianceDecision.RequiresManualReview(
            aiProposalId,
            reviewReason,
            requestedBy);

        await LogDecisionAsync(decision, requestedBy, cancellationToken);

        _logger.LogInformation("Requested manual review for customer {CustomerId}: {Reason}", customerId, reviewReason);

        return decision;
    }

    public async Task<ComplianceDecision> CompleteManualReviewAsync(
        ComplianceDecisionId decisionId,
        bool approved,
        string reviewerNotes,
        string reviewedBy,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ComplianceDecisions
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"Compliance decision {decisionId} not found");
        }

        if (entity.DecisionOutcome != ComplianceDecisionType.RequiresReview)
        {
            throw new InvalidOperationException($"Decision {decisionId} is not pending review");
        }

        entity.DecisionOutcome = approved 
            ? ComplianceDecisionType.Pass 
            : ComplianceDecisionType.Deny;
        entity.ReviewerNotes = reviewerNotes;
        entity.ReviewedBy = reviewedBy;
        entity.ReviewedAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Completed manual review for decision {DecisionId}: {Result}", 
            decisionId, approved ? "Approved" : "Denied");

        return MapToDomain(entity);
    }

    public async Task<IReadOnlyList<Mandate>> GetMandatesByCustomerAsync(
        CustomerId customerId,
        MandateStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Mandates.Where(m => m.CustomerId == customerId);

        if (status.HasValue)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        var entities = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<CompliancePolicy> CreatePolicyAsync(
        CompliancePolicyType policyType,
        string description,
        ComplianceEnforcementLevel enforcementLevel,
        bool requiresMandate = false,
        string? scopeSymbol = null,
        CancellationToken cancellationToken = default)
    {
        var policy = new CompliancePolicyEntity
        {
            Id = Guid.NewGuid(),
            PolicyType = policyType,
            Description = description,
            EnforcementLevel = enforcementLevel,
            RequiresMandate = requiresMandate,
            ScopeSymbol = scopeSymbol,
            IsActive = true,
            Version = 1,
            ExternalTimestampUtc = DateTime.UtcNow,
            ObservedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.CompliancePolicies.AddAsync(policy, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created compliance policy {PolicyId} type {PolicyType}", policy.Id, policyType);

        return MapToDomain(policy);
    }

    private async Task<(bool Passed, string Reason)> EvaluatePolicyAsync(
        CompliancePolicyEntity policy,
        CustomerId customerId,
        AccountId accountId,
        Symbol symbol,
        Side side,
        Quantity quantity,
        Price price,
        DateTime orderTimestamp,
        CancellationToken cancellationToken)
    {
        // Check scope filters
        if (policy.ScopeSymbol != null && policy.ScopeSymbol != symbol.Value)
            return (true, "Out of scope (symbol)");

        // Evaluate based on policy type
        switch (policy.PolicyType)
        {
            case CompliancePolicyType.TradingHoursRestriction:
                var hour = orderTimestamp.Hour;
                if (hour < 9 || hour >= 16) // Simplified market hours 9-16 UTC
                    return (false, "Order outside trading hours (9:00-16:00 UTC)");
                break;

            case CompliancePolicyType.AssetClassRestriction:
                // Simplified - would need asset class mapping
                if (symbol.Value.EndsWith(".FO")) // Assume .FO suffix means forbidden
                    return (false, $"Asset class restricted for symbol {symbol.Value}");
                break;

            case CompliancePolicyType.PatternDayTraderRule:
                // Simplified PDT check - would need trade count history
                var dayTradeCount = await GetDayTradeCountAsync(accountId, orderTimestamp, cancellationToken);
                if (dayTradeCount >= 4)
                    return (false, "Pattern day trader rule: 4+ day trades in 5 business days");
                break;
        }

        return (true, "Passed");
    }

    private async Task<int> GetDayTradeCountAsync(
        AccountId accountId,
        DateTime referenceDate,
        CancellationToken cancellationToken)
    {
        // Simplified implementation - would need proper day trade detection logic
        var fiveDaysAgo = referenceDate.AddDays(-5);
        
        var trades = await _dbContext.OrderExecutions
            .Where(e => e.OrderIntent.AccountId == accountId 
                     && e.FillTimestampUtc >= fiveDaysAgo
                     && e.FillTimestampUtc <= referenceDate)
            .CountAsync(cancellationToken);

        return trades;
    }

    private async Task<MandateEntity?> GetActiveMandateAsync(
        CustomerId customerId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Mandates
            .FirstOrDefaultAsync(m => m.CustomerId == customerId 
                                   && m.Status == MandateStatus.Active,
                cancellationToken);
    }

    private async Task LogDecisionAsync(
        ComplianceDecision decision,
        string? requestedBy,
        CancellationToken cancellationToken)
    {
        var entity = new ComplianceDecisionEntity
        {
            Id = decision.Id,
            AccountId = decision.AccountId ?? default,
            AiProposalId = decision.AiProposalId,
            DecisionType = decision.DecisionType,
            DecisionOutcome = decision.Outcome,
            Reason = decision.Reason,
            PolicyId = decision.PolicyId,
            PolicyVersion = decision.PolicyVersion,
            RequestedBy = requestedBy,
            ExternalTimestampUtc = decision.ExternalTimestampUtc,
            ObservedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.ComplianceDecisions.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Logged compliance decision {DecisionId}: {Outcome} - {Reason}",
            decision.Id, decision.Outcome, decision.Reason);
    }

    private Mandate MapToDomain(MandateEntity entity)
    {
        return new Mandate(
            entity.Id,
            entity.CustomerId,
            entity.MandateText,
            entity.Status,
            entity.IpAddress,
            entity.DeviceFingerprint)
        {
            AcceptedAtUtc = entity.AcceptedAtUtc,
            RevokedAtUtc = entity.RevokedAtUtc,
            RevocationReason = entity.RevocationReason,
            ExternalTimestampUtc = entity.ExternalTimestampUtc,
            ObservedAtUtc = entity.ObservedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private CompliancePolicy MapToDomain(CompliancePolicyEntity entity)
    {
        return new CompliancePolicy(
            entity.Id,
            entity.PolicyType,
            entity.Description,
            entity.EnforcementLevel,
            entity.RequiresMandate,
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

    private ComplianceDecision MapToDomain(ComplianceDecisionEntity entity)
    {
        return new ComplianceDecision(
            entity.Id,
            entity.AccountId,
            entity.AiProposalId,
            entity.DecisionType,
            entity.DecisionOutcome,
            entity.Reason,
            entity.PolicyId,
            entity.PolicyVersion)
        {
            RequestedBy = entity.RequestedBy,
            ReviewerNotes = entity.ReviewerNotes,
            ReviewedBy = entity.ReviewedBy,
            ReviewedAtUtc = entity.ReviewedAtUtc,
            ExternalTimestampUtc = entity.ExternalTimestampUtc,
            ObservedAtUtc = entity.ObservedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }
}
