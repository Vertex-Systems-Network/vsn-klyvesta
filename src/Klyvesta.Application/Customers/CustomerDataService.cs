using Klyvesta.Domain.Customers;

namespace Klyvesta.Application.Customers;

public sealed record CustomerProfileInput(
    string DisplayName,
    CustomerExperienceLevel ExperienceLevel,
    string InvestmentHorizon,
    string PrimaryGoal,
    decimal MonthlyContribution);

public sealed record CustomerGoalInput(
    Guid GoalId,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateOnly TargetDate,
    CustomerGoalStatus Status);

public interface ICustomerProfileStore
{
    ValueTask<CustomerWorkspace?> FindAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    ValueTask<CustomerWorkspace> CommitAsync(
        CustomerWorkspace candidate,
        long expectedRevision,
        CancellationToken cancellationToken = default);
}

public sealed class CustomerDataService
{
    private readonly ICustomerProfileStore _store;
    private readonly TimeProvider _timeProvider;

    public CustomerDataService(ICustomerProfileStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _timeProvider = timeProvider;
    }

    public async ValueTask<CustomerWorkspace?> GetAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        return await _store.FindAsync(customerId, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CustomerWorkspace> UpsertProfileAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        long expectedRevision,
        CustomerProfileInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureOwnership(authenticatedCustomerId, customerId);

        var current = await LoadForMutationAsync(customerId, expectedRevision, cancellationToken).ConfigureAwait(false);
        var version = current.Profile?.Version + 1 ?? 1;
        var profile = new CustomerProfile(
            customerId,
            version,
            input.DisplayName,
            input.ExperienceLevel,
            input.InvestmentHorizon,
            input.PrimaryGoal,
            input.MonthlyContribution,
            _timeProvider.GetUtcNow());
        var candidate = new CustomerWorkspace(
            customerId,
            current.Revision + 1,
            profile,
            current.RiskProfiles,
            current.Goals,
            current.Watchlist);

        return await _store.CommitAsync(candidate, expectedRevision, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CustomerWorkspace> RecalculateRiskAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        long expectedRevision,
        CustomerRiskAnswers answers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(answers);
        EnsureOwnership(authenticatedCustomerId, customerId);

        var current = await LoadForMutationAsync(customerId, expectedRevision, cancellationToken).ConfigureAwait(false);
        var version = current.LatestRiskProfile?.Version + 1 ?? 1;
        var risk = new CustomerRiskProfile(customerId, version, answers, _timeProvider.GetUtcNow());
        var candidate = new CustomerWorkspace(
            customerId,
            current.Revision + 1,
            current.Profile,
            current.RiskProfiles.Concat([risk]),
            current.Goals,
            current.Watchlist);

        return await _store.CommitAsync(candidate, expectedRevision, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CustomerWorkspace> AddGoalAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        long expectedRevision,
        CustomerGoalInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureOwnership(authenticatedCustomerId, customerId);

        var current = await LoadForMutationAsync(customerId, expectedRevision, cancellationToken).ConfigureAwait(false);
        if (current.Goals.Any(goal => goal.GoalId == input.GoalId))
        {
            throw new InvalidOperationException("CUSTOMER_GOAL_ALREADY_EXISTS");
        }

        var goal = new CustomerGoal(
            customerId,
            input.GoalId,
            input.Name,
            input.TargetAmount,
            input.CurrentAmount,
            input.TargetDate,
            input.Status,
            _timeProvider.GetUtcNow());
        var candidate = new CustomerWorkspace(
            customerId,
            current.Revision + 1,
            current.Profile,
            current.RiskProfiles,
            current.Goals.Concat([goal]),
            current.Watchlist);

        return await _store.CommitAsync(candidate, expectedRevision, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CustomerWorkspace> RemoveGoalAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        long expectedRevision,
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        if (goalId == Guid.Empty)
        {
            throw new ArgumentException("Goal ID is required.", nameof(goalId));
        }

        var current = await LoadForMutationAsync(customerId, expectedRevision, cancellationToken).ConfigureAwait(false);
        if (!current.Goals.Any(goal => goal.GoalId == goalId))
        {
            throw new InvalidOperationException("CUSTOMER_GOAL_NOT_FOUND");
        }

        var candidate = new CustomerWorkspace(
            customerId,
            current.Revision + 1,
            current.Profile,
            current.RiskProfiles,
            current.Goals.Where(goal => goal.GoalId != goalId),
            current.Watchlist);

        return await _store.CommitAsync(candidate, expectedRevision, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CustomerWorkspace> AddWatchlistSymbolAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        long expectedRevision,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        var normalized = CustomerWatchlistEntry.NormalizeSymbol(symbol);
        var current = await LoadForMutationAsync(customerId, expectedRevision, cancellationToken).ConfigureAwait(false);
        if (current.Watchlist.Any(entry => StringComparer.Ordinal.Equals(entry.Symbol, normalized)))
        {
            return current;
        }

        var entry = new CustomerWatchlistEntry(customerId, normalized, _timeProvider.GetUtcNow());
        var candidate = new CustomerWorkspace(
            customerId,
            current.Revision + 1,
            current.Profile,
            current.RiskProfiles,
            current.Goals,
            current.Watchlist.Concat([entry]));

        return await _store.CommitAsync(candidate, expectedRevision, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CustomerWorkspace> RemoveWatchlistSymbolAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        long expectedRevision,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        var normalized = CustomerWatchlistEntry.NormalizeSymbol(symbol);
        var current = await LoadForMutationAsync(customerId, expectedRevision, cancellationToken).ConfigureAwait(false);
        if (!current.Watchlist.Any(entry => StringComparer.Ordinal.Equals(entry.Symbol, normalized)))
        {
            throw new InvalidOperationException("CUSTOMER_WATCHLIST_SYMBOL_NOT_FOUND");
        }

        var candidate = new CustomerWorkspace(
            customerId,
            current.Revision + 1,
            current.Profile,
            current.RiskProfiles,
            current.Goals,
            current.Watchlist.Where(entry => !StringComparer.Ordinal.Equals(entry.Symbol, normalized)));

        return await _store.CommitAsync(candidate, expectedRevision, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<CustomerWorkspace> LoadForMutationAsync(
        Guid customerId,
        long expectedRevision,
        CancellationToken cancellationToken)
    {
        if (expectedRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedRevision), "Expected revision cannot be negative.");
        }

        var current = await _store.FindAsync(customerId, cancellationToken).ConfigureAwait(false)
            ?? new CustomerWorkspace(customerId, 0, profile: null);
        if (current.Revision != expectedRevision)
        {
            throw new InvalidOperationException("CUSTOMER_DATA_REVISION_CONFLICT");
        }

        return current;
    }

    private static void EnsureOwnership(Guid authenticatedCustomerId, Guid customerId)
    {
        if (authenticatedCustomerId == Guid.Empty)
        {
            throw new ArgumentException("Authenticated customer ID is required.", nameof(authenticatedCustomerId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        if (authenticatedCustomerId != customerId)
        {
            throw new InvalidOperationException("CUSTOMER_OWNERSHIP_MISMATCH");
        }
    }
}
