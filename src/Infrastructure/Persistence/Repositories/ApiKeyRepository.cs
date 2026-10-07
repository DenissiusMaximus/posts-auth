using auth.Domain.Entities;
using auth.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace auth.Infrastructure.Persistence.Repositories;

public sealed class ApiKeyRepository(AppDbContext dbContext) : IApiKeyRepository
{
    public Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ApiKeys.SingleOrDefaultAsync(apiKey => apiKey.Id == id, cancellationToken);

    public Task<ApiKey?> GetByPrefixAsync(
        string prefix,
        CancellationToken cancellationToken = default) =>
        dbContext.ApiKeys.SingleOrDefaultAsync(
            apiKey => apiKey.Prefix == prefix,
            cancellationToken);

    public async Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ApiKeys
            .AsNoTracking()
            .Where(apiKey => apiKey.UserId == userId)
            .OrderByDescending(apiKey => apiKey.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<ApiKey> Items, int TotalCount)> GetByUserIdPagedAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ApiKeys
            .AsNoTracking()
            .Where(apiKey => apiKey.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(apiKey => apiKey.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default) =>
        await dbContext.ApiKeys.AddAsync(apiKey, cancellationToken);

    public Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        dbContext.ApiKeys.Update(apiKey);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        dbContext.ApiKeys.Remove(apiKey);
        return Task.CompletedTask;
    }
}
