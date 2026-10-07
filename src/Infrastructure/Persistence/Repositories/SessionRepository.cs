using auth.Domain.Entities;
using auth.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace auth.Infrastructure.Persistence.Repositories;

public sealed class SessionRepository(AppDbContext dbContext) : ISessionRepository
{
    public Task<Session?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Sessions.SingleOrDefaultAsync(session => session.Id == id, cancellationToken);

    public Task<Session?> GetByRefreshTokenJtiAsync(
        string refreshTokenJti,
        CancellationToken cancellationToken = default) =>
        dbContext.Sessions.SingleOrDefaultAsync(
            session => session.RefreshTokenJti == refreshTokenJti,
            cancellationToken);

    public async Task<bool> RevokeIfActiveAsync(
        string refreshTokenJti,
        Guid userId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.Sessions
            .Where(session =>
                session.RefreshTokenJti == refreshTokenJti
                && session.UserId == userId
                && session.RevokedAtUtc == null
                && session.ExpiresAtUtc > revokedAtUtc)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    session => session.RevokedAtUtc,
                    revokedAtUtc),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<IReadOnlyList<Session>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.UserId == userId)
            .OrderByDescending(session => session.ExpiresAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Session session, CancellationToken cancellationToken = default) =>
        await dbContext.Sessions.AddAsync(session, cancellationToken);

    public Task UpdateAsync(Session session, CancellationToken cancellationToken = default)
    {
        dbContext.Sessions.Update(session);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Session session, CancellationToken cancellationToken = default)
    {
        dbContext.Sessions.Remove(session);
        return Task.CompletedTask;
    }
}
