using auth.Application.Abstractions;
using auth.Application.Commands.ApiKeys;
using auth.Domain;
using auth.Domain.Errors;
using auth.Domain.Repositories;
using MediatR;

namespace auth.Application.Handlers.ApiKeys;

public sealed class RevokeApiKeyHandler(
    IApiKeyRepository apiKeyRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<RevokeApiKeyCommand, Result>
{
    public async Task<Result> Handle(
        RevokeApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure(new Error(
                "Auth.Unauthorized",
                "An authenticated user is required.",
                ErrorType.Unauthorized));

        var apiKey = await apiKeyRepository.GetByIdAsync(request.KeyId, cancellationToken);
        if (apiKey is null || apiKey.UserId != userId)
        {
            return Result.Failure(new Error(
                "ApiKeys.NotFound",
                "The requested API key was not found.",
                ErrorType.NotFound));
        }

        if (apiKey.RevokedAtUtc is null)
        {
            apiKey.RevokedAtUtc = DateTime.UtcNow;
            await apiKeyRepository.UpdateAsync(apiKey, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
