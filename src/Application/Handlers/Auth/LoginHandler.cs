using auth.Application.Commands.Auth;
using auth.Application.Abstractions;
using auth.Domain;
using auth.Domain.Entities;
using auth.Domain.Errors;
using auth.Domain.Repositories;
using MediatR;

namespace auth.Application.Handlers.Auth;

public sealed class LoginHandler(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IPasswordHasher passwordHasher,
    IJwtProvider jwtProvider,
    IUnitOfWork unitOfWork) : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByLoginAsync(request.Login.Trim(), cancellationToken);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return new Error(
                "Auth.InvalidCredentials",
                "The login or password is invalid.",
                ErrorType.Unauthorized);
        }

        var (accessToken, _) = jwtProvider.GenerateAccessToken(user.Id);
        var (refreshToken, refreshTokenJti, refreshExpiresAtUtc) =
            jwtProvider.GenerateRefreshToken(user.Id);

        await sessionRepository.AddAsync(new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RefreshTokenJti = refreshTokenJti,
            ExpiresAtUtc = refreshExpiresAtUtc,
            RevokedAtUtc = null
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponse(accessToken, refreshToken);
    }
}
