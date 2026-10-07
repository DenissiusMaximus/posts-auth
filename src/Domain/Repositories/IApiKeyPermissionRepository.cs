using auth.Domain.Entities;

namespace auth.Domain.Repositories;

public interface IApiKeyPermissionRepository
{
    Task<ApiKeyPermission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiKeyPermission?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKeyPermission>> GetAllAsync(
        CancellationToken cancellationToken = default);
}
