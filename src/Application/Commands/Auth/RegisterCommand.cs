using auth.Domain;
using MediatR;

namespace auth.Application.Commands.Auth;

public sealed record RegisterCommand(string Login, string Password) : IRequest<Result<LoginResponse>>;
