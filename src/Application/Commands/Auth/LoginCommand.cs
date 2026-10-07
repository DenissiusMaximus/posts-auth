using auth.Domain;
using MediatR;

namespace auth.Application.Commands.Auth;

public sealed record LoginCommand(string Login, string Password) : IRequest<Result<LoginResponse>>;

public sealed record LoginResponse(string AccessToken, string RefreshToken);

