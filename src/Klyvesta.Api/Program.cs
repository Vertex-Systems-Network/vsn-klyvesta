using Microsoft.AspNetCore.Diagnostics.HealthChecks;

const string DemoCookieName = "klyvesta_demo_preview";

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
        await next();
    });

    app.UseStaticFiles();

    app.MapGet("/", () => Results.Redirect("/demo/login.html"));

    app.MapGet("/api/demo/status", (IWebHostEnvironment environment) => Results.Ok(new
    {
        mode = "demo-preview",
        environment = environment.EnvironmentName,
        database = "bypassed",
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

    app.MapGet("/api/demo/dashboard", (HttpContext context) =>
    {
        if (!context.Request.Cookies.TryGetValue(DemoCookieName, out var demoCookie)
            || !string.Equals(demoCookie, "active", StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new
        {
            asOf = DateTimeOffset.UtcNow,
            account = new
            {
                name = "Demo Investor",
                accountId = "KLY-DEMO-001",
                mode = "AI Assisted — Preview",
                cash = 387_500m,
                portfolioValue = 1_842_750m,
                dayChange = 18_425m,
                dayChangePercent = 1.01m,
            },
            holdings = new[]
            {
                new { symbol = "HBL", name = "Habib Bank Limited", quantity = 2400, price = 126.40m, value = 303_360m, changePercent = 1.42m },
                new { symbol = "LUCK", name = "Lucky Cement", quantity = 510, price = 918.75m, value = 468_562.50m, changePercent = 0.74m },
                new { symbol = "SYS", name = "Systems Limited", quantity = 1500, price = 548.20m, value = 822_300m, changePercent = 1.91m },
                new { symbol = "MEBL", name = "Meezan Bank", quantity = 910, price = 273.11m, value = 248_530.10m, changePercent = -0.36m },
            },
            watchlist = new[]
            {
                new { symbol = "OGDC", price = 232.14m, changePercent = 0.62m },
                new { symbol = "ENGROH", price = 214.32m, changePercent = -0.18m },
                new { symbol = "FFC", price = 414.90m, changePercent = 1.08m },
                new { symbol = "PSO", price = 368.70m, changePercent = 0.41m },
            },
            orders = new[]
            {
                new { symbol = "SYS", side = "BUY", quantity = 250, type = "LIMIT", status = "PAPER FILLED", price = 541.50m },
                new { symbol = "HBL", side = "BUY", quantity = 400, type = "LIMIT", status = "PAPER FILLED", price = 124.10m },
                new { symbol = "MEBL", side = "SELL", quantity = 100, type = "LIMIT", status = "PAPER OPEN", price = 276.00m },
            },
            aiInsight = new
            {
                title = "Portfolio concentration check",
                message = "Synthetic preview: technology exposure is elevated versus the sample target. Review diversification before taking any action.",
                confidence = "Demo only — not investment advice",
            },
            safeguards = new[]
            {
                "No database connection",
                "No pyPSX credentials or network calls",
                "No real orders or funds",
                "Synthetic market and portfolio data only",
            },
        });
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

internal sealed record DemoLoginRequest(string? Email, string? Password);
