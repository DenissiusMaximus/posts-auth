using auth.Domain.Entities;

namespace auth.Domain.Repositories;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Session?> GetByRefreshTokenJtiAsync(
        string refreshTokenJti,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeIfActiveAsync(
        string refreshTokenJti,
        Guid userId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Session>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Session session, CancellationToken cancellationToken = default);

    Task UpdateAsync(Session session, CancellationToken cancellationToken = default);

    Task DeleteAsync(Session session, CancellationToken cancellationToken = default);
}
