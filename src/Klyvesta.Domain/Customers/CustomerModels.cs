using System.Collections.ObjectModel;

namespace Klyvesta.Domain.Customers;

public enum CustomerExperienceLevel
{
    Unknown = 0,
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3,
}

public enum CustomerRiskBand
{
    Unknown = 0,
    Conservative = 1,
    Balanced = 2,
    Growth = 3,
    HighGrowth = 4,
}

public enum CustomerGoalStatus
{
    Unknown = 0,
    Active = 1,
    Paused = 2,
    Completed = 3,
}

public sealed class CustomerRiskAnswers
{
    public CustomerRiskAnswers(int lossTolerance, int marketExperience, int horizonCapacity, int liquidityNeed)
    {
        ValidateAnswer(lossTolerance, nameof(lossTolerance));
        ValidateAnswer(marketExperience, nameof(marketExperience));
        ValidateAnswer(horizonCapacity, nameof(horizonCapacity));
        ValidateAnswer(liquidityNeed, nameof(liquidityNeed));

        LossTolerance = lossTolerance;
        MarketExperience = marketExperience;
        HorizonCapacity = horizonCapacity;
        LiquidityNeed = liquidityNeed;
    }

    public int LossTolerance { get; }

    public int MarketExperience { get; }

    public int HorizonCapacity { get; }

    public int LiquidityNeed { get; }

    private static void ValidateAnswer(int value, string parameterName)
    {
        if (value is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Risk answers must be between 1 and 5.");
        }
    }
}

public sealed class CustomerProfile
{
    public CustomerProfile(
        Guid customerId,
        int version,
        string displayName,
        CustomerExperienceLevel experienceLevel,
        string investmentHorizon,
        string primaryGoal,
        decimal monthlyContribution,
        DateTimeOffset updatedAt)
    {
        CustomerValidation.RequireCustomerId(customerId, nameof(customerId));
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Profile version must be at least 1.");
        }

        if (experienceLevel == CustomerExperienceLevel.Unknown)
        {
            throw new ArgumentException("Experience level is required.", nameof(experienceLevel));
        }

        CustomerValidation.RequireMoney(monthlyContribution, allowZero: true, nameof(monthlyContribution));

        CustomerId = customerId;
        Version = version;
        DisplayName = CustomerValidation.NormalizeText(displayName, 120, nameof(displayName));
        ExperienceLevel = experienceLevel;
        InvestmentHorizon = CustomerValidation.NormalizeText(investmentHorizon, 80, nameof(investmentHorizon));
        PrimaryGoal = CustomerValidation.NormalizeText(primaryGoal, 160, nameof(primaryGoal));
        MonthlyContribution = monthlyContribution;
        UpdatedAt = updatedAt;
    }

    public Guid CustomerId { get; }

    public int Version { get; }

    public string DisplayName { get; }

    public CustomerExperienceLevel ExperienceLevel { get; }

    public string InvestmentHorizon { get; }

    public string PrimaryGoal { get; }

    public decimal MonthlyContribution { get; }

    public DateTimeOffset UpdatedAt { get; }
}

public sealed class CustomerRiskProfile
{
    public CustomerRiskProfile(
        Guid customerId,
        int version,
        CustomerRiskAnswers answers,
        DateTimeOffset updatedAt)
    {
        CustomerValidation.RequireCustomerId(customerId, nameof(customerId));
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Risk profile version must be at least 1.");
        }

        ArgumentNullException.ThrowIfNull(answers);

        CustomerId = customerId;
        Version = version;
        Answers = answers;
        Score = CalculateScore(answers);
        RiskBand = Score switch
        {
            <= 40 => CustomerRiskBand.Conservative,
            <= 65 => CustomerRiskBand.Balanced,
            <= 80 => CustomerRiskBand.Growth,
            _ => CustomerRiskBand.HighGrowth,
        };
        UpdatedAt = updatedAt;
    }

    public Guid CustomerId { get; }

    public int Version { get; }

    public CustomerRiskAnswers Answers { get; }

    public int Score { get; }

    public CustomerRiskBand RiskBand { get; }

    public DateTimeOffset UpdatedAt { get; }

    private static int CalculateScore(CustomerRiskAnswers answers) =>
        (
            answers.LossTolerance * 30
            + answers.MarketExperience * 20
            + answers.HorizonCapacity * 30
            + (6 - answers.LiquidityNeed) * 20
        ) / 5;
}

public sealed class CustomerGoal
{
    public CustomerGoal(
        Guid customerId,
        Guid goalId,
        string name,
        decimal targetAmount,
        decimal currentAmount,
        DateOnly targetDate,
        CustomerGoalStatus status,
        DateTimeOffset createdAt)
    {
        CustomerValidation.RequireCustomerId(customerId, nameof(customerId));
        if (goalId == Guid.Empty)
        {
            throw new ArgumentException("Goal ID is required.", nameof(goalId));
        }

        CustomerValidation.RequireMoney(targetAmount, allowZero: false, nameof(targetAmount));
        CustomerValidation.RequireMoney(currentAmount, allowZero: true, nameof(currentAmount));
        if (status == CustomerGoalStatus.Unknown)
        {
            throw new ArgumentException("Goal status is required.", nameof(status));
        }

        CustomerId = customerId;
        GoalId = goalId;
        Name = CustomerValidation.NormalizeText(name, 120, nameof(name));
        TargetAmount = targetAmount;
        CurrentAmount = currentAmount;
        TargetDate = targetDate;
        Status = status;
        CreatedAt = createdAt;
    }

    public Guid CustomerId { get; }

    public Guid GoalId { get; }

    public string Name { get; }

    public decimal TargetAmount { get; }

    public decimal CurrentAmount { get; }

    public DateOnly TargetDate { get; }

    public CustomerGoalStatus Status { get; }

    public DateTimeOffset CreatedAt { get; }
}

public sealed class CustomerWatchlistEntry
{
    public CustomerWatchlistEntry(Guid customerId, string symbol, DateTimeOffset addedAt)
    {
        CustomerValidation.RequireCustomerId(customerId, nameof(customerId));

        CustomerId = customerId;
        Symbol = NormalizeSymbol(symbol);
        AddedAt = addedAt;
    }

    public Guid CustomerId { get; }

    public string Symbol { get; }

    public DateTimeOffset AddedAt { get; }

    public static string NormalizeSymbol(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var normalized = symbol.Trim().ToUpperInvariant();
        if (normalized.Length is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(symbol), "Watchlist symbol length must be between 1 and 16 characters.");
        }

        if (normalized.Any(character =>
                !(character is >= 'A' and <= 'Z'
                  || character is >= '0' and <= '9'
                  || character is '.' or '-')))
        {
            throw new ArgumentException("Watchlist symbol contains unsupported characters.", nameof(symbol));
        }

        return normalized;
    }
}

public sealed class CustomerWorkspace
{
    private readonly ReadOnlyCollection<CustomerRiskProfile> _riskProfiles;
    private readonly ReadOnlyCollection<CustomerGoal> _goals;
    private readonly ReadOnlyCollection<CustomerWatchlistEntry> _watchlist;

    public CustomerWorkspace(
        Guid customerId,
        long revision,
        CustomerProfile? profile,
        IEnumerable<CustomerRiskProfile>? riskProfiles = null,
        IEnumerable<CustomerGoal>? goals = null,
        IEnumerable<CustomerWatchlistEntry>? watchlist = null)
    {
        CustomerValidation.RequireCustomerId(customerId, nameof(customerId));
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), "Workspace revision cannot be negative.");
        }

        if (profile is not null && profile.CustomerId != customerId)
        {
            throw new ArgumentException("Profile owner does not match workspace customer.", nameof(profile));
        }

        var risks = riskProfiles?.ToArray() ?? [];
        var customerGoals = goals?.ToArray() ?? [];
        var entries = watchlist?.ToArray() ?? [];

        RequireOwnership(customerId, risks.Select(risk => risk.CustomerId), nameof(riskProfiles));
        RequireOwnership(customerId, customerGoals.Select(goal => goal.CustomerId), nameof(goals));
        RequireOwnership(customerId, entries.Select(entry => entry.CustomerId), nameof(watchlist));

        for (var index = 0; index < risks.Length; index++)
        {
            if (risks[index].Version != index + 1)
            {
                throw new ArgumentException("Risk profile versions must be contiguous and ordered from version 1.", nameof(riskProfiles));
            }
        }

        if (customerGoals.Select(goal => goal.GoalId).Distinct().Count() != customerGoals.Length)
        {
            throw new ArgumentException("Goal IDs must be unique within a customer workspace.", nameof(goals));
        }

        if (entries.Select(entry => entry.Symbol).Distinct(StringComparer.Ordinal).Count() != entries.Length)
        {
            throw new ArgumentException("Watchlist symbols must be unique within a customer workspace.", nameof(watchlist));
        }

        CustomerId = customerId;
        Revision = revision;
        Profile = profile;
        _riskProfiles = Array.AsReadOnly(risks);
        _goals = Array.AsReadOnly(customerGoals);
        _watchlist = Array.AsReadOnly(entries);
    }

    public Guid CustomerId { get; }

    public long Revision { get; }

    public CustomerProfile? Profile { get; }

    public IReadOnlyList<CustomerRiskProfile> RiskProfiles => _riskProfiles;

    public IReadOnlyList<CustomerGoal> Goals => _goals;

    public IReadOnlyList<CustomerWatchlistEntry> Watchlist => _watchlist;

    public CustomerRiskProfile? LatestRiskProfile => _riskProfiles.Count == 0 ? null : _riskProfiles[^1];

    private static void RequireOwnership(Guid customerId, IEnumerable<Guid> owners, string parameterName)
    {
        if (owners.Any(owner => owner != customerId))
        {
            throw new ArgumentException("Workspace child owner does not match workspace customer.", parameterName);
        }
    }
}

internal static class CustomerValidation
{
    internal static void RequireCustomerId(Guid customerId, string parameterName)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", parameterName);
        }
    }

    internal static string NormalizeText(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Text length cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    internal static void RequireMoney(decimal amount, bool allowZero, string parameterName)
    {
        if (amount < 0m || (!allowZero && amount == 0m))
        {
            throw new ArgumentOutOfRangeException(parameterName, allowZero ? "Amount cannot be negative." : "Amount must be greater than zero.");
        }

        var scale = (decimal.GetBits(amount)[3] >> 16) & 0x7F;
        if (scale > 2)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Amount supports at most two decimal places.");
        }
    }
}
