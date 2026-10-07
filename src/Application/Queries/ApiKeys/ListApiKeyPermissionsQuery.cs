using auth.Domain;
using MediatR;

namespace auth.Application.Queries.ApiKeys;

public sealed record ListApiKeyPermissionsQuery
    : IRequest<Result<ListApiKeyPermissionsResponse>>;

public sealed record ApiKeyPermissionDto(Guid Id, string Name);

public sealed record ListApiKeyPermissionsResponse(
    IReadOnlyList<ApiKeyPermissionDto> Permissions);
