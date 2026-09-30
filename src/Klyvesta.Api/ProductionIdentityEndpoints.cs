using Klyvesta.Infrastructure.Persistence.Identity;

namespace Klyvesta.Api;

public static class ProductionIdentityEndpoints
{
    private const string CookieName = "klyvesta_session";

    public static void MapProductionIdentity(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (
            IdentityRegisterRequest request,
            HttpContext context,
            IdentityCredentialStore credentials,
            IdentitySessionStore sessions,
            CancellationToken cancellationToken) =>
        {
            var userId = await credentials.RegisterAsync(
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                DateTimeOffset.UtcNow,
                cancellationToken);
            if (userId is null)
            {
                return Results.BadRequest(new { error = "REGISTRATION_INVALID_OR_EMAIL_EXISTS" });
            }

            await IssueSession(context, userId.Value, sessions, cancellationToken);
            return Results.Created("/api/auth/me", new { authenticated = true, userId });
        });

        app.MapPost("/api/auth/login", async (
            IdentityLoginRequest request,
            HttpContext context,
            IdentityCredentialStore credentials,
            IdentitySessionStore sessions,
            CancellationToken cancellationToken) =>
        {
            var userId = await credentials.AuthenticateAsync(
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                cancellationToken);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            await IssueSession(context, userId.Value, sessions, cancellationToken);
            return Results.Ok(new { authenticated = true });
        });

        app.MapPost("/api/auth/logout", async (
            HttpContext context,
            IdentitySessionStore sessions,
            CancellationToken cancellationToken) =>
        {
            if (context.Request.Cookies.TryGetValue(CookieName, out var token))
            {
                await sessions.RevokeAsync(token, DateTimeOffset.UtcNow, cancellationToken);
            }

            context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
            return Results.Ok(new { authenticated = false });
        });

        app.MapGet("/api/auth/me", async (
            HttpContext context,
            IdentitySessionStore sessions,
            CancellationToken cancellationToken) =>
        {
            if (!context.Request.Cookies.TryGetValue(CookieName, out var token))
            {
                return Results.Unauthorized();
            }

            var userId = await sessions.FindActiveUserAsync(
                token,
                DateTimeOffset.UtcNow,
                cancellationToken);
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(new { authenticated = true, userId });
        });
    }

    private static async Task IssueSession(
        HttpContext context,
        Guid userId,
        IdentitySessionStore sessions,
        CancellationToken cancellationToken)
    {
        var token = await sessions.CreateAsync(
            userId,
            DateTimeOffset.UtcNow,
            TimeSpan.FromHours(8),
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            cancellationToken);

        context.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(8),
            Path = "/",
        });
    }
}

public sealed record IdentityRegisterRequest(string? Email, string? Password);
public sealed record IdentityLoginRequest(string? Email, string? Password);
