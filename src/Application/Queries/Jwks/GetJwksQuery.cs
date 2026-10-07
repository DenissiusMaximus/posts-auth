using auth.Domain;
using MediatR;

namespace auth.Application.Queries.Jwks;

public sealed record GetJwksQuery() : IRequest<Result<GetJwksResponse>>;

public sealed record JsonWebKey(
    string Kty,
    string Use,
    string Kid,
    string Alg,
    string N,
    string E);

public sealed record GetJwksResponse(IReadOnlyList<JsonWebKey> Keys);

