using Klyvesta.Application;
using Klyvesta.Infrastructure;
using Klyvesta.Infrastructure.Services;
using Klyvesta.Infrastructure.Adapters;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddEnvironmentVariables();
var brokerMode = builder.Configuration.GetValue<string>("Broker:Mode") ?? "Paper";
var liveTradingEnabled = builder.Configuration.GetValue<bool>("FeatureFlags:LiveTradingEnabled", false);
var paperTradingEnabled = builder.Configuration.GetValue<bool>("FeatureFlags:PaperTradingEnabled", true);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Klyvesta Trading API", Version = "v1" });
});

// Application Layer
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SubmitOrderIntentHandler).Assembly));

// Infrastructure Services - Core
builder.Services.AddSingleton<IRiskGovernor, RiskGovernor>();
builder.Services.AddSingleton<IComplianceGate, ComplianceGate>();
builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();

// Broker Adapter - Polymorphic based on configuration
if (brokerMode.Equals("Live", StringComparison.OrdinalIgnoreCase) && liveTradingEnabled)
{
    builder.Services.AddScoped<IBrokerAdapter, LiveBrokerAdapter>();
    Console.WriteLine("🔴 LIVE BROKER MODE ENABLED - Real trading active");
}
else
{
    builder.Services.AddScoped<IBrokerAdapter, PaperBrokerAdapter>();
    Console.WriteLine("🟡 PAPER BROKER MODE - Simulation only");
}

// Additional Services (when implemented)
// builder.Services.AddScoped<IMandateService, MandateService>();
// builder.Services.AddScoped<IOnboardingService, OnboardingService>();
// builder.Services.AddScoped<IMarketDataService, MarketDataService>();
// builder.Services.AddScoped<AiShadowModeService>();

// Database
builder.Services.AddDbContext<KlyvestaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Rate Limiting
var rateLimitEnabled = builder.Configuration.GetValue<bool>("RateLimiting:Enabled", true);
var maxRequestsPerMinute = builder.Configuration.GetValue<int>("RateLimiting:MaxRequestsPerMinute", 100);
if (rateLimitEnabled)
{
    builder.Services.AddMemoryCache();
    // Rate limiting middleware will be added below
}

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection"), name: "database")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", name: "redis");

// Prometheus Metrics
builder.Services.AddMetricServer();

var app = builder.Build();

// Middleware Pipeline
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Rate Limiting Middleware
if (rateLimitEnabled)
{
    app.UseMiddleware<RateLimitMiddleware>();
}

// Payload Signing Middleware (for sensitive endpoints)
var signingKey = builder.Configuration.GetValue<string>("Security:PayloadSigningKey");
if (!string.IsNullOrEmpty(signingKey) && signingKey != "ChangeMeInProduction")
{
    app.UseMiddleware<PayloadSigningMiddleware>();
}

// Prometheus Metrics
app.UseHttpMetrics();

// Swagger (Development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

// Health Endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static _ => false,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name != "self",
});

// Metrics Endpoint for Prometheus
app.MapMetrics("/metrics");

// Root endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "klyvesta-api",
    version = "1.0.0",
    mode = brokerMode,
    status = "operational",
    features = new {
        paperTrading = paperTradingEnabled,
        liveTrading = liveTradingEnabled && brokerMode.Equals("Live", StringComparison.OrdinalIgnoreCase)
    },
    timestamp = DateTime.UtcNow
}));

Console.WriteLine($"✅ Klyvesta API starting in {brokerMode} mode");
Console.WriteLine($"📊 Prometheus metrics: http://localhost:5000/metrics");
Console.WriteLine($"🏥 Health checks: http://localhost:5000/health/ready");

app.Run();
