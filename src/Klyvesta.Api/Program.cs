using Microsoft.AspNetCore.Diagnostics.HealthChecks;

const string DemoCookieName = "klyvesta_demo_preview";
const string DemoMutationHeader = "X-Demo-Request";

var builder = WebApplication.CreateBuilder(args);

var demoEnabled = builder.Configuration.GetValue<bool>("DemoMode:Enabled");
var demoEnvironmentAllowed =
    builder.Environment.IsDevelopment()
    || builder.Environment.IsEnvironment("Demo");

if (demoEnabled && !demoEnvironmentAllowed)
{
    throw new InvalidOperationException(
        "DemoMode may only run in the Development or Demo environment. Production must remain fail-closed.");
}

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
if (demoEnabled)
{
    builder.Services.AddSingleton<DemoUserDataStore>();
}

var app = builder.Build();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Demo"))
{
    app.UseHsts();
}

if (demoEnabled)
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Response.Headers.Append("Pragma", "no-cache");
        context.Response.Headers.Append("X-Robots-Tag", "noindex, nofollow, noarchive");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "no-referrer");
        context.Response.Headers.Append(
            "Content-Security-Policy",
            "default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'");
        await next();
    });

    app.UseStaticFiles();

    app.MapGet("/", () => Results.Redirect("/demo/login.html"));

    app.MapGet("/api/demo/status", (IWebHostEnvironment environment) => Results.Ok(new
    {
        mode = "demo-preview",
        environment = environment.EnvironmentName,
        database = "bypassed",
        userDataStore = "local-json",
        broker = "synthetic-paper-data",
        pypsx = "not-connected",
        realMoney = false,
        productionAuthority = false,
    }));

    app.MapPost("/api/demo/login", (DemoLoginRequest request, HttpContext context, IConfiguration configuration) =>
    {
        var configuredEmail = configuration["DemoMode:Email"] ?? "demo@klyvesta.local";
        var configuredPassword = configuration["DemoMode:Password"] ?? "demo";

        if (!string.Equals(request.Email?.Trim(), configuredEmail, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.Password, configuredPassword, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        context.Response.Cookies.Append(DemoCookieName, "active", new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            MaxAge = TimeSpan.FromHours(8),
            Path = "/",
        });

        return Results.Ok(new
        {
            authenticated = true,
            redirect = "/demo/dashboard.html",
            warning = "Demo preview only. This is not production authentication.",
        });
    });

    app.MapPost("/api/demo/logout", (HttpContext context) =>
    {
        context.Response.Cookies.Delete(DemoCookieName, new CookieOptions { Path = "/" });
        return Results.Ok(new { authenticated = false });
    });

    app.MapGet("/api/demo/dashboard", async (
        HttpContext context,
        DemoUserDataStore store,
        CancellationToken cancellationToken) =>
    {
        if (!IsDemoAuthenticated(context))
        {
            return Results.Unauthorized();
        }

        var state = await store.GetAsync(cancellationToken);
        var holdings = state.PaperPortfolio.Holdings
            .Select(holding => new
            {
                symbol = holding.Symbol,
                name = holding.Name,
                quantity = holding.Quantity,
                price = holding.Price,
                value = holding.Value,
                changePercent = holding.ChangePercent,
            })
            .ToArray();
        var holdingsValue = state.PaperPortfolio.Holdings.Sum(holding => holding.Value);
        var portfolioValue = state.PaperPortfolio.Cash + holdingsValue;
        var dayChange = CalculateDayChange(state.PaperPortfolio.Holdings);
        var dayChangePercent = portfolioValue - dayChange == 0m
            ? 0m
            : dayChange / (portfolioValue - dayChange) * 100m;
        var watchlist = state.WatchlistSymbols
            .Where(DemoMarketCatalog.Contains)
            .Select(DemoMarketCatalog.Get)
            .ToArray();
        var insight = BuildAiInsight(state, portfolioValue);

        return Results.Ok(new
        {
            asOf = DateTimeOffset.UtcNow,
            account = new
            {
                name = state.Profile.DisplayName,
                accountId = "KLY-DEMO-001",
                mode = "AI Assisted — Preview",
                cash = state.PaperPortfolio.Cash,
                portfolioValue,
                dayChange,
                dayChangePercent,
            },
            profile = state.Profile,
            riskProfile = state.RiskProfile,
            goals = state.Goals,
            holdings,
            watchlist,
            availableWatchlistSymbols = DemoMarketCatalog.Symbols,
            orders = state.PaperPortfolio.Orders,
            activity = state.Activity.Take(12),
            aiInsight = insight,
            safeguards = DemoPreviewData.Safeguards,
        });
    });

    app.MapGet("/api/demo/profile", async (
        HttpContext context,
        DemoUserDataStore store,
        CancellationToken cancellationToken) =>
    {
        if (!IsDemoAuthenticated(context))
        {
            return Results.Unauthorized();
        }

        var state = await store.GetAsync(cancellationToken);
        return Results.Ok(state.Profile);
    });

    app.MapPut("/api/demo/profile", async (
        DemoProfileUpdateRequest request,
        HttpContext context,
        DemoUserDataStore store,
        CancellationToken cancellationToken) =>
    {
        if (!IsDemoMutationAuthorized(context))
        {
            return Results.Unauthorized();
        }

        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        var experienceLevel = request.ExperienceLevel?.Trim() ?? string.Empty;
        var investmentHorizon = request.InvestmentHorizon?.Trim() ?? string.Empty;
        var primaryGoal = request.PrimaryGoal?.Trim() ?? string.Empty;
        if (!HasLength(displayName, 2, 80)
            || !HasLength(experienceLevel, 2, 40)
            || !HasLength(investmentHorizon, 2, 40)
            || !HasLength(primaryGoal, 2, 120)
            || request.MonthlyContribution is < 0m or > 100_000_000m)
        {
            return Results.BadRequest(new { error = "PROFILE_INPUT_INVALID" });
        }

        var state = await store.UpdateProfileAsync(
            new DemoInvestorProfile(
                displayName,
                experienceLevel,
                investmentHorizon,
                primaryGoal,
                request.MonthlyContribution),
            cancellationToken);
        return Results.Ok(state.Profile);
    });

    app.MapPut("/api/demo/risk-profile", async (
        DemoRiskAnswers request,
        HttpContext context,
        DemoUserDataStore store,
        CancellationToken cancellationToken) =>
    {
        if (!IsDemoMutationAuthorized(context))
        {
            return Results.Unauthorized();
        }

        if (!IsRiskAnswerValid(request.LossTolerance)
            || !IsRiskAnswerValid(request.MarketExperience)
            || !IsRiskAnswerValid(request.HorizonCapacity)
            || !IsRiskAnswerValid(request.LiquidityNeed))
        {
            return Results.BadRequest(new { error = "RISK_ANSWER_INVALID", allowedRange = "1-5" });
        }

        var state = await store.UpdateRiskAnswersAsync(request, cancellationToken);
        return Results.Ok(state.RiskProfile);
    });

    app.MapGet("/api/demo/goals", async (
        HttpContext context,
        DemoUserDataStore store,
        CancellationToken cancellationToken) =>
    {
        if (!IsDemoAuthenticated(context))
        {
            return Results.Unauthorized();
        }

        var state = await store.GetAsync(cancellationToken);
        return Results.Ok(state.Goals);
    });

    app.MapPost("/api/demo/goals", async (
        DemoGoalCreateRequest request,
        HttpContext context,
        DemoUserDataStore store,
        CancellationToken cancellationToken) =>
    {
        if (!IsDemoMutationAuthorized(context))
        {
            return Results.Unauthorized();
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (!HasLength(name, 2, 100)
            || request.TargetAmount <= 0m
            || request.TargetAmount > 1_000_000_000_000m
            || request.CurrentAmount < 0m
            || request.CurrentAmount > request.TargetAmount
            || !DateOnly.TryParse(request.TargetDate, out _))
        {
            return Results.BadRequest(new { error = "GOAL_INPUT_INVALID" });
        }

        var goal = new DemoGoal(
            Guid.CreateVersion7(),
            name,
            request.TargetAmount,
            request.CurrentAmount,
            request.TargetDate!,
            "active");
        var state = await store.AddGoalAsync(goal, cancellationToken);
        return Results.Ok(state.Goals);
    });

    app.MapDelete("/api/demo/goals/{goalId:guid}", async (
        Guid goalId,
        HttpContext context,
        DemoUserDataStore store,
        CancellationToken cancellationToken) =>
    {
        if (!IsDemoMutationAuthorized(context))
        {
            return Results.Unauthorized();
        }

        var state = await store.RemoveGoalAsync(goalId, cancellationToken);
        return Results.Ok(state.Goals);
    });

    app.MapPost("/api/demo/watchlist", async (
        DemoWatchlistRequest request,
        HttpContext context,
        DemoUserDataStore store,
        CancellationToken cancellationToken) =>
    {
        if (!IsDemoMutationAuthorized(context))
        {
            return Results.Unauthorized();
        }

        var symbol = request.Symbol?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!DemoMarketCatalog.Contains(symbol))
        {
            return Results.BadRequest(new
            {
                error = "WATCHLIST_SYMBOL_INVALID",
                allowedSymbols = DemoMarketCatalog.Symbols,
            });
        }

        var state = await store.AddWatchlistSymbolAsync(symbol, cancellationToken);
        return Results.Ok(state.WatchlistSymbols.Select(DemoMarketCatalog.Get));
    });

    app.MapDelete("/api/demo/watchlist/{symbol}", async (
        string symbol,
        HttpContext context,
        DemoUserDataStore store,
        CancellationToken cancellationToken) =>
    {
        if (!IsDemoMutationAuthorized(context))
        {
            return Results.Unauthorized();
        }

        var normalized = symbol.Trim().ToUpperInvariant();
        if (!DemoMarketCatalog.Contains(normalized))
        {
            return Results.BadRequest(new { error = "WATCHLIST_SYMBOL_INVALID" });
        }

        var state = await store.RemoveWatchlistSymbolAsync(normalized, cancellationToken);
        return Results.Ok(state.WatchlistSymbols.Select(DemoMarketCatalog.Get));
    });

    app.MapPost("/api/demo/reset", async (
        HttpContext context,
        DemoUserDataStore store,
        CancellationToken cancellationToken) =>
    {
        if (!IsDemoMutationAuthorized(context))
        {
            return Results.Unauthorized();
        }

        await store.ResetAsync(cancellationToken);
        return Results.Ok(new { reset = true });
    });
}
else
{
    app.MapGet("/", () => Results.Ok(new
    {
        service = "klyvesta-api",
        status = "foundation",
        demoMode = false,
    }));
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static _ => false,
});

app.MapHealthChecks("/health/ready");

app.Run();

static bool IsDemoAuthenticated(HttpContext context) =>
    context.Request.Cookies.TryGetValue(DemoCookieName, out var demoCookie)
    && string.Equals(demoCookie, "active", StringComparison.Ordinal);

static bool IsDemoMutationAuthorized(HttpContext context) =>
    IsDemoAuthenticated(context)
    && context.Request.Headers.TryGetValue(DemoMutationHeader, out var header)
    && string.Equals(header.ToString(), "1", StringComparison.Ordinal);

static bool HasLength(string value, int minimum, int maximum) =>
    value.Length >= minimum && value.Length <= maximum;

static bool IsRiskAnswerValid(int value) => value is >= 1 and <= 5;

static decimal CalculateDayChange(IReadOnlyList<DemoPaperHolding> holdings)
{
    var total = 0m;
    foreach (var holding in holdings)
    {
        var denominator = 1m + holding.ChangePercent / 100m;
        if (denominator <= 0m)
        {
            continue;
        }

        var previousValue = holding.Value / denominator;
        total += holding.Value - previousValue;
    }

    return decimal.Round(total, 2, MidpointRounding.AwayFromZero);
}

static DemoAiInsight BuildAiInsight(DemoUserState state, decimal portfolioValue)
{
    var topHolding = state.PaperPortfolio.Holdings
        .OrderByDescending(holding => holding.Value)
        .FirstOrDefault();
    if (topHolding is null || portfolioValue <= 0m)
    {
        return new DemoAiInsight(
            "Portfolio context incomplete",
            "Add paper holdings before generating a synthetic concentration insight.",
            "Demo only — not investment advice");
    }

    var concentration = decimal.Round(topHolding.Value / portfolioValue * 100m, 1, MidpointRounding.AwayFromZero);
    return new DemoAiInsight(
        "Profile-aware concentration check",
        $"Synthetic preview: {topHolding.Symbol} is {concentration}% of modeled NAV. Your demo risk band is {state.RiskProfile.RiskBand}. Review diversification and goal fit before taking any action.",
        "Deterministic demo insight — not suitability, advice, or a live recommendation");
}

internal sealed record DemoLoginRequest(string? Email, string? Password);

internal sealed record DemoProfileUpdateRequest(
    string? DisplayName,
    string? ExperienceLevel,
    string? InvestmentHorizon,
    string? PrimaryGoal,
    decimal MonthlyContribution);

internal sealed record DemoGoalCreateRequest(
    string? Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    string? TargetDate);

internal sealed record DemoWatchlistRequest(string? Symbol);

internal sealed record DemoAiInsight(string Title, string Message, string Confidence);

internal static class DemoPreviewData
{
    internal static readonly string[] Safeguards =
    [
        "No PostgreSQL connection",
        "No pyPSX credentials or network calls",
        "No real orders, deposits, withdrawals, or funds",
        "Synthetic profile, risk, market, and portfolio data only",
        "Local JSON demo state contains no production KYC or restricted PII",
    ];
}
