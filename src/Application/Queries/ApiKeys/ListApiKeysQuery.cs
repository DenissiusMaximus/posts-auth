using auth.Domain;
using MediatR;

namespace auth.Application.Queries.ApiKeys;

public sealed record ListApiKeysQuery : IRequest<Result<ListApiKeysResponse>>;

public sealed record ApiKeyDto(Guid Id, string Name, DateTime CreatedAtUtc, bool IsActive);

public sealed record ListApiKeysResponse(IReadOnlyList<ApiKeyDto> Keys);

public sealed record ListApiKeysPagedQuery(int Page, int PageSize)
    : IRequest<Result<ListApiKeysPagedResponse>>;

public sealed record ListApiKeysPagedResponse(
    IReadOnlyList<ApiKeyDto> Keys,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
