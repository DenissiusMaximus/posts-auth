using auth.Domain;
using MediatR;

namespace auth.Application.Commands.Auth;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;

