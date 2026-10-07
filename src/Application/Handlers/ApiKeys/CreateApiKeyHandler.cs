using auth.Application.Commands.ApiKeys;
using auth.Application.Abstractions;
using auth.Domain;
using auth.Domain.Entities;
using auth.Domain.Errors;
using auth.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace auth.Application.Handlers.ApiKeys;

public sealed class CreateApiKeyHandler(
    IApiKeyRepository apiKeyRepository,
    IApiKeyPermissionRepository apiKeyPermissionRepository,
    IUserRepository userRepository,
    IApiKeyGenerator apiKeyGenerator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<CreateApiKeyCommand, Result<CreateApiKeyResponse>>
{
    public async Task<Result<CreateApiKeyResponse>> Handle(
        CreateApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
        {
            return new Error(
                "Auth.Unauthorized",
                "An authenticated user is required.",
                ErrorType.Unauthorized);
        }

        if (await userRepository.GetByIdAsync(userId, cancellationToken) is null)
        {
            return new Error(
                "Users.NotFound",
                "The requested user was not found.",
                ErrorType.NotFound);
        }

        var permission = await apiKeyPermissionRepository.GetByNameAsync(
            "readwrite",
            cancellationToken);
        if (permission is null)
        {
            return new Error(
                "ApiKeys.PermissionNotConfigured",
                "The default API key permission is not configured.",
                ErrorType.Failure);
        }

        var generatedApiKey = apiKeyGenerator.Generate(Guid.NewGuid());
        await apiKeyRepository.AddAsync(new ApiKey
        {
            Id = generatedApiKey.Id,
            UserId = userId,
            Name = request.Name.Trim(),
            Prefix = generatedApiKey.Prefix,
            SecretHash = generatedApiKey.SecretHash,
            Last4 = generatedApiKey.Last4,
            ApiKeyPermissionId = permission.Id,
            CreatedAtUtc = DateTime.UtcNow,
            RevokedAtUtc = null
        }, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateApiKeyName(exception))
        {
            return new Error(
                "ApiKeys.NameAlreadyExists",
                "An API key with this name already exists for the user.",
                ErrorType.Conflict);
        }

        return new CreateApiKeyResponse(generatedApiKey.Id, generatedApiKey.Key);
    }

    private static bool IsDuplicateApiKeyName(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            TableName: "api_keys",
            ConstraintName: "IX_api_keys_UserId_Name"
        };
}
