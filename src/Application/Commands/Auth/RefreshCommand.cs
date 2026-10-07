using auth.Domain;
using MediatR;

namespace auth.Application.Commands.Auth;

public sealed record RefreshCommand(string RefreshToken) : IRequest<Result<RefreshResponse>>;

public sealed record RefreshResponse(string AccessToken, string RefreshToken);

