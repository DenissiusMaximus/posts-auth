using auth.Application.Queries.ApiKeys;
using auth.Domain;
using auth.Domain.Repositories;
using MediatR;

namespace auth.Application.Handlers.ApiKeys;

public sealed class ListApiKeyPermissionsHandler(
    IApiKeyPermissionRepository apiKeyPermissionRepository)
    : IRequestHandler<ListApiKeyPermissionsQuery, Result<ListApiKeyPermissionsResponse>>
{
    public async Task<Result<ListApiKeyPermissionsResponse>> Handle(
        ListApiKeyPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var permissions = await apiKeyPermissionRepository.GetAllAsync(cancellationToken);
        var response = permissions
            .Select(permission => new ApiKeyPermissionDto(permission.Id, permission.Name))
            .ToArray();

        return new ListApiKeyPermissionsResponse(response);
    }
}
