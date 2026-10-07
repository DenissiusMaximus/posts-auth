using auth.Domain.Entities;
using auth.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace auth.Infrastructure.Persistence.Repositories;

public sealed class ApiKeyPermissionRepository(AppDbContext dbContext) : IApiKeyPermissionRepository
{
    public Task<ApiKeyPermission?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        dbContext.ApiKeyPermissions
            .SingleOrDefaultAsync(permission => permission.Id == id, cancellationToken);

    public Task<ApiKeyPermission?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        dbContext.ApiKeyPermissions
            .SingleOrDefaultAsync(permission => permission.Name == name, cancellationToken);

    public async Task<IReadOnlyList<ApiKeyPermission>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.ApiKeyPermissions
            .AsNoTracking()
            .OrderBy(permission => permission.Name)
            .ToListAsync(cancellationToken);
}
