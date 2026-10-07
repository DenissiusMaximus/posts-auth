using auth.Domain;
using MediatR;

namespace auth.Application.Commands.Auth;

public sealed record ExchangeApiKeyCommand(string ApiKey) : IRequest<Result<ExchangeApiKeyResponse>>;

public sealed record ExchangeApiKeyResponse(string AccessToken);

