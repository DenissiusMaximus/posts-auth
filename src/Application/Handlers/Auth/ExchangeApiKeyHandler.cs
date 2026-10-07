using auth.Application.Commands.Auth;
using auth.Application.Abstractions;
using auth.Domain;
using auth.Domain.Errors;
using auth.Domain.Repositories;
using MediatR;

namespace auth.Application.Handlers.Auth;

public sealed class ExchangeApiKeyHandler(
    IApiKeyRepository apiKeyRepository,
    IApiKeyGenerator apiKeyGenerator,
    IJwtProvider jwtProvider,
    IUnitOfWork unitOfWork) : IRequestHandler<ExchangeApiKeyCommand, Result<ExchangeApiKeyResponse>>
{
    public async Task<Result<ExchangeApiKeyResponse>> Handle(
        ExchangeApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        var parts = request.ApiKey.Split('_', 3, StringSplitOptions.None);
        if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return new Error(
                "ApiKeys.Invalid",
                "The provided API key is invalid.",
                ErrorType.Unauthorized);
        }

        var apiKey = await apiKeyRepository.GetByPrefixAsync(
            $"{parts[0]}_{parts[1]}",
            cancellationToken);
        if (apiKey is null
            || apiKey.RevokedAtUtc is not null
            || !apiKeyGenerator.Verify(apiKey.Id, request.ApiKey, apiKey.SecretHash))
        {
            return new Error(
                "ApiKeys.Invalid",
                "The provided API key is invalid or revoked.",
                ErrorType.Unauthorized);
        }

        apiKey.LastUsedAtUtc = DateTime.UtcNow;
        await apiKeyRepository.UpdateAsync(apiKey, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var (accessToken, _) = jwtProvider.GenerateAccessToken(apiKey.UserId);
        return new ExchangeApiKeyResponse(accessToken);
    }
}
