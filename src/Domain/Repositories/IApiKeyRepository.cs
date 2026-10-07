using auth.Domain.Entities;

namespace auth.Domain.Repositories;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiKey?> GetByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ApiKey> Items, int TotalCount)> GetByUserIdPagedAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(ApiKey apiKey, CancellationToken cancellationToken = default);
}
