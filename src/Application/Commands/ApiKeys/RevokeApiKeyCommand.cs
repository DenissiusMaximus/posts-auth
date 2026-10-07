using auth.Domain;
using MediatR;

namespace auth.Application.Commands.ApiKeys;

public sealed record RevokeApiKeyCommand(Guid KeyId) : IRequest<Result>;
