using auth.Application.Abstractions;
using auth.Application.Queries.ApiKeys;
using auth.Domain;
using auth.Domain.Repositories;
using MediatR;

namespace auth.Application.Handlers.ApiKeys;

public sealed class ListApiKeysHandler(
    IApiKeyRepository apiKeyRepository,
    ICurrentUser currentUser) : IRequestHandler<ListApiKeysQuery, Result<ListApiKeysResponse>>
{
    public async Task<Result<ListApiKeysResponse>> Handle(
        ListApiKeysQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return new auth.Domain.Errors.Error(
                "Auth.Unauthorized",
                "An authenticated user is required.",
                auth.Domain.Errors.ErrorType.Unauthorized);

        var apiKeys = await apiKeyRepository.GetByUserIdAsync(userId, cancellationToken);
        var response = apiKeys
            .Select(apiKey => new ApiKeyDto(
                apiKey.Id,
                apiKey.Name,
                apiKey.CreatedAtUtc,
                apiKey.RevokedAtUtc is null))
            .ToArray();

        return new ListApiKeysResponse(response);
    }
}

public sealed class ListApiKeysPagedHandler(
    IApiKeyRepository apiKeyRepository,
    ICurrentUser currentUser)
    : IRequestHandler<ListApiKeysPagedQuery, Result<ListApiKeysPagedResponse>>
{
    public async Task<Result<ListApiKeysPagedResponse>> Handle(
        ListApiKeysPagedQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return new auth.Domain.Errors.Error(
                "Auth.Unauthorized",
                "An authenticated user is required.",
                auth.Domain.Errors.ErrorType.Unauthorized);

        var (apiKeys, totalCount) = await apiKeyRepository.GetByUserIdPagedAsync(
            userId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var response = apiKeys
            .Select(apiKey => new ApiKeyDto(
                apiKey.Id,
                apiKey.Name,
                apiKey.CreatedAtUtc,
                apiKey.RevokedAtUtc is null))
            .ToArray();

        return new ListApiKeysPagedResponse(
            response,
            request.Page,
            request.PageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)request.PageSize));
    }
}
