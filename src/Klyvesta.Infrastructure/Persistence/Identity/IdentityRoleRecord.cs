namespace Klyvesta.Infrastructure.Persistence.Identity;

internal sealed class IdentityRoleRecord
{
    public Guid UserId { get; set; }
    public required string Role { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
}