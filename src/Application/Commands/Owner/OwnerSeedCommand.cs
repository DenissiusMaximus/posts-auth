using auth.Domain;
using MediatR;

namespace auth.Application.Commands.Owner;

public sealed record OwnerSeedCommand(string Login, string Password) : IRequest<Result>;

