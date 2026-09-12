using System.Text.Json;
using Klyvesta.Application.Handlers;
using Klyvesta.Infrastructure.Persistence;
using Klyvesta.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klyvesta.Infrastructure.Services;

/// <summary>
/// Implementation of idempotency service for duplicate command detection.
/// </summary>
public sealed class IdempotencyService : IIdempotencyService
{
    private readonly KlyvestaDbContext _dbContext;
    private readonly ILogger<IdempotencyService> _logger;
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(24);

    public IdempotencyService(KlyvestaDbContext dbContext, ILogger<IdempotencyService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<T?> GetExistingResultAsync<T>(string idempotencyKey, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();

        var record = await _dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey && r.ExpiresAt > DateTimeOffset.UtcNow, ct);

        if (record == null)
        {
            return null;
        }

        if (record.State != "Completed")
        {
            // Operation still in progress - caller should wait or retry
            _logger.LogInformation(
                "Idempotency key {Key} found with state {State} - operation in progress",
                idempotencyKey, record.State);
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(record.State, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            _logger.LogDebug(
                "Found existing result for idempotency key {Key}",
                idempotencyKey);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to deserialize stored result for idempotency key {Key}",
                idempotencyKey);
            return null;
        }
    }

    public async Task StoreResultAsync<T>(string idempotencyKey, T result, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();

        // Check if already exists
        var existing = await _dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey, ct);

        if (existing != null)
        {
            _logger.LogDebug(
                "Idempotency key {Key} already exists, skipping store",
                idempotencyKey);
            return;
        }

        var record = new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            Scope = typeof(T).Name,
            Key = idempotencyKey,
            RequestHash = GenerateHash(idempotencyKey),
            State = "Completed",
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow + _defaultExpiry
        };

        // Serialize result - we'll store it in a separate table or use JSON column
        // For now, we're using a simplified approach
        // In production, you'd want a proper ResultData table with polymorphic serialization
        
        _dbContext.IdempotencyKeys.Add(record);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Stored idempotency record for key {Key} with scope {Scope}",
                idempotencyKey, record.Scope);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to store idempotency record for key {Key}",
                idempotencyKey);
            throw;
        }
    }

    private static string GenerateHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
