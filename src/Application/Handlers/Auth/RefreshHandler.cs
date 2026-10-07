using auth.Application.Commands.Auth;
using auth.Application.Abstractions;
using auth.Domain;
using auth.Domain.Entities;
using auth.Domain.Errors;
using auth.Domain.Repositories;
using MediatR;

namespace auth.Application.Handlers.Auth;

public sealed class RefreshHandler(
    ISessionRepository sessionRepository,
    IJwtProvider jwtProvider,
    IUnitOfWork unitOfWork) : IRequestHandler<RefreshCommand, Result<RefreshResponse>>
{
    public async Task<Result<RefreshResponse>> Handle(
        RefreshCommand request,
        CancellationToken cancellationToken)
    {
        var validation = jwtProvider.ValidateRefreshToken(request.RefreshToken);
        if (validation.IsFailure)
            return validation.Error;

        var session = await sessionRepository.GetByRefreshTokenJtiAsync(
            validation.Value.Jti,
            cancellationToken);
        if (session is null
            || session.UserId != validation.Value.SubjectId
            || session.RevokedAtUtc is not null
            || session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return new Error(
                "Auth.RefreshTokenInvalid",
                "The provided refresh token is invalid or expired.",
                ErrorType.Unauthorized);
        }

        var revokedAtUtc = DateTime.UtcNow;
        RefreshResponse? response = null;
        await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var revoked = await sessionRepository.RevokeIfActiveAsync(
                validation.Value.Jti,
                session.UserId,
                revokedAtUtc,
                transactionCancellationToken);
            if (!revoked)
                return;

            var (accessToken, _) = jwtProvider.GenerateAccessToken(session.UserId);
            var (refreshToken, refreshTokenJti, refreshExpiresAtUtc) =
                jwtProvider.GenerateRefreshToken(session.UserId);

            await sessionRepository.AddAsync(new Session
            {
                Id = Guid.NewGuid(),
                UserId = session.UserId,
                RefreshTokenJti = refreshTokenJti,
                ExpiresAtUtc = refreshExpiresAtUtc,
                RevokedAtUtc = null
            }, transactionCancellationToken);
            await unitOfWork.SaveChangesAsync(transactionCancellationToken);
            response = new RefreshResponse(accessToken, refreshToken);
        }, cancellationToken);

        return response is null
            ? new Error(
                "Auth.RefreshTokenInvalid",
                "The provided refresh token is invalid or expired.",
                ErrorType.Unauthorized)
            : response;
    }
}
