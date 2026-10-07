using auth.Domain;
using MediatR;

namespace auth.Application.Commands.ApiKeys;

public sealed record CreateApiKeyCommand(string Name) : IRequest<Result<CreateApiKeyResponse>>;

public sealed record CreateApiKeyResponse(Guid Id, string Key);
