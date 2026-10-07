using auth.Application.Commands.Auth;
using auth.Application.Abstractions;
using auth.Domain;
using auth.Domain.Errors;
using auth.Domain.Repositories;
using MediatR;

namespace auth.Application.Handlers.Auth;

public sealed class LogoutHandler(
    ISessionRepository sessionRepository,
    IJwtProvider jwtProvider,
    IUnitOfWork unitOfWork) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var validation = jwtProvider.ValidateRefreshToken(request.RefreshToken);
        if (validation.IsFailure)
            return Result.Failure(validation.Error);

        var session = await sessionRepository.GetByRefreshTokenJtiAsync(
            validation.Value.Jti,
            cancellationToken);
        if (session is null || session.UserId != validation.Value.SubjectId)
        {
            return Result.Failure(new Error(
                "Auth.RefreshTokenInvalid",
                "The provided refresh token is invalid or expired.",
                ErrorType.Unauthorized));
        }

        if (session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            await sessionRepository.UpdateAsync(session, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
