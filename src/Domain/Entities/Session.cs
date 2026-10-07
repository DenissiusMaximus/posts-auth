namespace auth.Domain.Entities;

public class Session
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string RefreshTokenJti { get; set; } = null!;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
}
