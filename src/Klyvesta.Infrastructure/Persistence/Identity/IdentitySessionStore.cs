using Microsoft.EntityFrameworkCore;

namespace Klyvesta.Infrastructure.Persistence.Identity;

public sealed class IdentitySessionStore(
    KlyvestaDbContext dbContext,
    OpaqueSessionTokenService tokenService)
{
    public async Task<string> CreateAsync(
        Guid userId,
        DateTimeOffset now,
        TimeSpan lifetime,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        var token = tokenService.CreateToken();
        dbContext.IdentitySessions.Add(new IdentitySessionRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = OpaqueSessionTokenService.HashToken(token),
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
            IpAddress = ipAddress,
            UserAgent = userAgent,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<Guid?> FindActiveUserAsync(
        string token,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tokenHash = OpaqueSessionTokenService.HashToken(token);
        var session = await dbContext.IdentitySessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TokenHash == tokenHash
                    && item.RevokedAt == null
                    && item.ExpiresAt > now,
                cancellationToken);

        return session?.UserId;
    }

    public async Task RevokeAsync(
        string token,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tokenHash = OpaqueSessionTokenService.HashToken(token);
        var session = await dbContext.IdentitySessions
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.RevokedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}