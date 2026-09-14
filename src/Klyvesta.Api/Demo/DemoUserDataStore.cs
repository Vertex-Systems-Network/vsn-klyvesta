using System.Text.Json;

internal sealed class DemoUserDataStore
{
    private const int MaxActivityEvents = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _dataFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DemoUserState? _cachedState;

    public DemoUserDataStore(IWebHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredPath = configuration["DemoMode:DataFile"];
        _dataFilePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "demo-user-state.json")
            : Path.GetFullPath(configuredPath, environment.ContentRootPath);
    }

    public Task<DemoUserState> GetAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(static state => state, persist: false, cancellationToken);

    public Task<DemoUserState> UpdateProfileAsync(
        DemoInvestorProfile profile,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            state => state with
            {
                Profile = profile,
                Activity = AppendActivity(state.Activity, "PROFILE_UPDATED", "Investor profile updated."),
            },
            persist: true,
            cancellationToken);

    public Task<DemoUserState> UpdateRiskAnswersAsync(
        DemoRiskAnswers answers,
        CancellationToken cancellationToken = default)
    {
        var riskProfile = DemoRiskScoring.Score(answers, DateTimeOffset.UtcNow);
        return MutateAsync(
            state => state with
            {
                RiskProfile = riskProfile,
                Activity = AppendActivity(
                    state.Activity,
                    "RISK_PROFILE_UPDATED",
                    $"Demo risk indicator recalculated as {riskProfile.RiskBand}."),
            },
            persist: true,
            cancellationToken);
    }

    public Task<DemoUserState> AddGoalAsync(
        DemoGoal goal,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            state => state with
            {
                Goals = state.Goals.Concat([goal]).ToArray(),
                Activity = AppendActivity(state.Activity, "GOAL_ADDED", $"Goal added: {goal.Name}."),
            },
            persist: true,
            cancellationToken);

    public Task<DemoUserState> RemoveGoalAsync(
        Guid goalId,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            state => state with
            {
                Goals = state.Goals.Where(goal => goal.Id != goalId).ToArray(),
                Activity = AppendActivity(state.Activity, "GOAL_REMOVED", "Goal removed from demo profile."),
            },
            persist: true,
            cancellationToken);

    public Task<DemoUserState> AddWatchlistSymbolAsync(
        string symbol,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            state => state.WatchlistSymbols.Contains(symbol, StringComparer.Ordinal)
                ? state
                : state with
                {
                    WatchlistSymbols = state.WatchlistSymbols.Concat([symbol]).ToArray(),
                    Activity = AppendActivity(state.Activity, "WATCHLIST_ADDED", $"{symbol} added to watchlist."),
                },
            persist: true,
            cancellationToken);

    public Task<DemoUserState> RemoveWatchlistSymbolAsync(
        string symbol,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            state => state with
            {
                WatchlistSymbols = state.WatchlistSymbols
                    .Where(existing => !StringComparer.Ordinal.Equals(existing, symbol))
                    .ToArray(),
                Activity = AppendActivity(state.Activity, "WATCHLIST_REMOVED", $"{symbol} removed from watchlist."),
            },
            persist: true,
            cancellationToken);

    public async Task<DemoUserState> ResetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _cachedState = DemoUserState.CreateSeed();
            await PersistAsync(_cachedState, cancellationToken);
            return _cachedState;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DemoUserState> MutateAsync(
        Func<DemoUserState, DemoUserState> mutation,
        bool persist,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await LoadAsync(cancellationToken);
            var updated = mutation(current);
            _cachedState = updated;
            if (persist)
            {
                await PersistAsync(updated, cancellationToken);
            }

            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DemoUserState> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cachedState is not null)
        {
            return _cachedState;
        }

        if (!File.Exists(_dataFilePath))
        {
            _cachedState = DemoUserState.CreateSeed();
            await PersistAsync(_cachedState, cancellationToken);
            return _cachedState;
        }

        try
        {
            await using var stream = File.OpenRead(_dataFilePath);
            _cachedState = await JsonSerializer.DeserializeAsync<DemoUserState>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Demo user data file did not contain a valid state object.");
            return _cachedState;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Demo user data file is invalid JSON. Remove or repair the demo data file before continuing.",
                exception);
        }
    }

    private async Task PersistAsync(DemoUserState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_dataFilePath)
            ?? throw new InvalidOperationException("Demo data file path must include a directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = _dataFilePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, _dataFilePath, overwrite: true);
    }

    private static IReadOnlyList<DemoActivityEvent> AppendActivity(
        IReadOnlyList<DemoActivityEvent> existing,
        string code,
        string message)
    {
        DemoActivityEvent next = new(code, message, DateTimeOffset.UtcNow);
        return existing
            .Prepend(next)
            .Take(MaxActivityEvents)
            .ToArray();
    }
}

internal sealed record DemoUserState(
    DemoInvestorProfile Profile,
    DemoRiskProfile RiskProfile,
    IReadOnlyList<DemoGoal> Goals,
    IReadOnlyList<string> WatchlistSymbols,
    DemoPaperPortfolio PaperPortfolio,
    IReadOnlyList<DemoActivityEvent> Activity)
{
    internal static DemoUserState CreateSeed()
    {
        var createdAt = DateTimeOffset.UtcNow;
        return new DemoUserState(
            new DemoInvestorProfile(
                "Demo Investor",
                "Intermediate",
                "5-7 years",
                "Long-term wealth growth",
                50_000m),
            DemoRiskScoring.Score(new DemoRiskAnswers(3, 3, 4, 2), createdAt),
            [
                new DemoGoal(
                    Guid.Parse("09f023e9-9346-42f5-a8b2-32ea6ee2784a"),
                    "Long-term portfolio",
                    5_000_000m,
                    2_230_252.60m,
                    "2031-12-31",
                    "active"),
            ],
            ["OGDC", "ENGROH", "FFC", "PSO"],
            new DemoPaperPortfolio(
                387_500m,
                [
                    new DemoPaperHolding("HBL", "Habib Bank Limited", 2400m, 126.40m, 1.42m),
                    new DemoPaperHolding("LUCK", "Lucky Cement", 510m, 918.75m, 0.74m),
                    new DemoPaperHolding("SYS", "Systems Limited", 1500m, 548.20m, 1.91m),
                    new DemoPaperHolding("MEBL", "Meezan Bank", 910m, 273.11m, -0.36m),
                ],
                [
                    new DemoPaperOrder("SYS", "BUY", 250m, "LIMIT", "PAPER FILLED", 541.50m),
                    new DemoPaperOrder("HBL", "BUY", 400m, "LIMIT", "PAPER FILLED", 124.10m),
                    new DemoPaperOrder("MEBL", "SELL", 100m, "LIMIT", "PAPER OPEN", 276.00m),
                ]),
            [
                new DemoActivityEvent(
                    "DEMO_PROFILE_CREATED",
                    "Synthetic investor profile created for the local demo environment.",
                    createdAt),
            ]);
    }
}

internal sealed record DemoInvestorProfile(
    string DisplayName,
    string ExperienceLevel,
    string InvestmentHorizon,
    string PrimaryGoal,
    decimal MonthlyContribution);

internal sealed record DemoRiskAnswers(
    int LossTolerance,
    int MarketExperience,
    int HorizonCapacity,
    int LiquidityNeed);

internal sealed record DemoRiskProfile(
    DemoRiskAnswers Answers,
    int Score,
    string RiskBand,
    DateTimeOffset UpdatedAt);

internal sealed record DemoGoal(
    Guid Id,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    string TargetDate,
    string Status);

internal sealed record DemoPaperPortfolio(
    decimal Cash,
    IReadOnlyList<DemoPaperHolding> Holdings,
    IReadOnlyList<DemoPaperOrder> Orders);

internal sealed record DemoPaperHolding(
    string Symbol,
    string Name,
    decimal Quantity,
    decimal Price,
    decimal ChangePercent)
{
    internal decimal Value => Quantity * Price;
}

internal sealed record DemoPaperOrder(
    string Symbol,
    string Side,
    decimal Quantity,
    string Type,
    string Status,
    decimal Price);

internal sealed record DemoActivityEvent(
    string Code,
    string Message,
    DateTimeOffset OccurredAt);

internal static class DemoRiskScoring
{
    internal static DemoRiskProfile Score(DemoRiskAnswers answers, DateTimeOffset updatedAt)
    {
        ValidateAnswer(answers.LossTolerance, nameof(answers.LossTolerance));
        ValidateAnswer(answers.MarketExperience, nameof(answers.MarketExperience));
        ValidateAnswer(answers.HorizonCapacity, nameof(answers.HorizonCapacity));
        ValidateAnswer(answers.LiquidityNeed, nameof(answers.LiquidityNeed));

        var weighted =
            answers.LossTolerance * 30
            + answers.MarketExperience * 20
            + answers.HorizonCapacity * 30
            + (6 - answers.LiquidityNeed) * 20;
        var score = weighted / 5;
        var band = score switch
        {
            <= 40 => "Conservative",
            <= 65 => "Balanced",
            <= 80 => "Growth",
            _ => "High growth",
        };

        return new DemoRiskProfile(answers, score, band, updatedAt);
    }

    private static void ValidateAnswer(int value, string parameterName)
    {
        if (value is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Demo risk answers must be between 1 and 5.");
        }
    }
}

internal static class DemoMarketCatalog
{
    private static readonly IReadOnlyDictionary<string, DemoMarketQuote> Quotes =
        new Dictionary<string, DemoMarketQuote>(StringComparer.Ordinal)
        {
            ["HBL"] = new("HBL", 126.40m, 1.42m),
            ["LUCK"] = new("LUCK", 918.75m, 0.74m),
            ["SYS"] = new("SYS", 548.20m, 1.91m),
            ["MEBL"] = new("MEBL", 273.11m, -0.36m),
            ["OGDC"] = new("OGDC", 232.14m, 0.62m),
            ["ENGROH"] = new("ENGROH", 214.32m, -0.18m),
            ["FFC"] = new("FFC", 414.90m, 1.08m),
            ["PSO"] = new("PSO", 368.70m, 0.41m),
        };

    internal static bool Contains(string symbol) => Quotes.ContainsKey(symbol);

    internal static DemoMarketQuote Get(string symbol) => Quotes[symbol];

    internal static IReadOnlyList<string> Symbols => Quotes.Keys.Order(StringComparer.Ordinal).ToArray();
}

internal sealed record DemoMarketQuote(string Symbol, decimal Price, decimal ChangePercent);
