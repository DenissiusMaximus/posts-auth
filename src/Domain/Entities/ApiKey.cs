namespace auth.Domain.Entities;

public class ApiKey
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Prefix { get; set; } = null!;

    public string SecretHash { get; set; } = null!;

    public string Last4 { get; set; } = null!;

    public Guid ApiKeyPermissionId { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
}
