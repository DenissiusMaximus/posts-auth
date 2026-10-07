using auth.Application.Commands.Auth;
using auth.Application.Abstractions;
using auth.Domain;
using auth.Domain.Entities;
using auth.Domain.Errors;
using auth.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace auth.Application.Handlers.Auth;

public sealed class RegisterHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ISessionRepository sessionRepository,
    IJwtProvider jwtProvider,
    IUnitOfWork unitOfWork) : IRequestHandler<RegisterCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var login = request.Login.Trim();
        if (await userRepository.GetByLoginAsync(login, cancellationToken) is not null)
        {
            return Result.Failure<LoginResponse>(new Error(
                "Auth.LoginAlreadyExists",
                "A user with this login already exists.",
                ErrorType.Conflict));
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Login = login,
            PasswordHash = passwordHasher.Hash(request.Password)
        };

        var (accessToken, _) = jwtProvider.GenerateAccessToken(user.Id);
        var (refreshToken, refreshTokenJti, refreshExpiresAtUtc) =
            jwtProvider.GenerateRefreshToken(user.Id);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                await userRepository.AddAsync(user, transactionCancellationToken);
                await sessionRepository.AddAsync(new Session
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RefreshTokenJti = refreshTokenJti,
                    ExpiresAtUtc = refreshExpiresAtUtc,
                    RevokedAtUtc = null
                }, transactionCancellationToken);
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateLogin(exception))
        {
            return Result.Failure<LoginResponse>(new Error(
                "Auth.LoginAlreadyExists",
                "A user with this login already exists.",
                ErrorType.Conflict));
        }

        return new LoginResponse(accessToken, refreshToken);
    }

    private static bool IsDuplicateLogin(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            TableName: "users",
            ConstraintName: "IX_users_Login"
        };
}
