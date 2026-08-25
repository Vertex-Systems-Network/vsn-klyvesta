using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Klyvesta.Infrastructure.Persistence;

public sealed class KlyvestaDbContextFactory : IDesignTimeDbContextFactory<KlyvestaDbContext>
{
    public KlyvestaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("KLYVESTA_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "KLYVESTA_DB_CONNECTION must be set for design-time persistence operations. " +
                "Do not commit database credentials to repository configuration.");
        }

        var options = new DbContextOptionsBuilder<KlyvestaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new KlyvestaDbContext(options);
    }
}
