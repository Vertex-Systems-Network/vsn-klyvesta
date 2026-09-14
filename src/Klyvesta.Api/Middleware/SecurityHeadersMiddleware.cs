using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Klyvesta.Api.Middleware;

/// <summary>
/// Adds comprehensive security headers to all HTTP responses.
/// Protects against XSS, clickjacking, MIME-type sniffing, and other common web vulnerabilities.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Prevent MIME-type sniffing
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            
            // Prevent clickjacking attacks
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            
            // Enable XSS filter in browsers
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            
            // Control referrer information
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            
            // Content Security Policy - restrict resource loading
            // For API-only services, this is restrictive but can be customized if needed
            context.Response.Headers.Append(
                "Content-Security-Policy", 
                "default-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'"
            );
            
            // Permissions Policy - disable unnecessary browser features
            context.Response.Headers.Append(
                "Permissions-Policy",
                "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()"
            );
            
            // HSTS - enforce HTTPS (only in production)
            if (!context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                context.Response.Headers.Append(
                    "Strict-Transport-Security",
                    "max-age=31536000; includeSubDomains; preload"
                );
            }

            _logger.LogDebug("Security headers added to response for {Path}", context.Request.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add security headers to response for {Path}", context.Request.Path);
            // Don't fail the request if headers can't be set - log and continue
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to register security headers middleware
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
