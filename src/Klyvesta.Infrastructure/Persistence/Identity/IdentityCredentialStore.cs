using Microsoft.EntityFrameworkCore;

namespace Klyvesta.Infrastructure.Persistence.Identity;

public sealed class IdentityCredentialStore(
    KlyvestaDbContext dbContext,
    IdentityPasswordHasher passwordHasher)
{
    public async Task<Guid?> RegisterAsync(
        string email,
        string password,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null || password.Length < 12)
        {
            return null;
        }

        var exists = await dbContext.IdentityUsers
            .AnyAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);
        if (exists)
        {
            return null;
        }

        var userId = Guid.CreateVersion7();
        dbContext.IdentityUsers.Add(new IdentityUserRecord
        {
            Id = userId,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHasher.Hash(password),
            Status = "active",
            CreatedAt = now,
        });
        dbContext.IdentityRoles.Add(new IdentityRoleRecord
        {
            UserId = userId,
            Role = "user",
            GrantedAt = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return userId;
    }

    public async Task<Guid?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null)
        {
            return null;
        }

        var user = await dbContext.IdentityUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);
        return user is not null
            && user.Status == "active"
            && passwordHasher.Verify(password, user.PasswordHash)
            ? user.Id
            : null;
    }

    private static string? NormalizeEmail(string email)
    {
        var value = email.Trim();
        return value.Length is < 3 or > 320 || !value.Contains('@')
            ? null
            : value.ToUpperInvariant();
    }
}