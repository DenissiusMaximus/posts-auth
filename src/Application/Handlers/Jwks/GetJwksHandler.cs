using auth.Application.Abstractions;
using auth.Application.Queries.Jwks;
using auth.Domain;
using MediatR;

namespace auth.Application.Handlers.Jwks;

public sealed class GetJwksHandler(IJwtProvider jwtProvider) : IRequestHandler<GetJwksQuery, Result<GetJwksResponse>>
{
    public Task<Result<GetJwksResponse>> Handle(GetJwksQuery request, CancellationToken cancellationToken)
    {
        var key = jwtProvider.GetPublicJwk();
        var response = new GetJwksResponse(
        [
            new JsonWebKey(key.Kty, key.Use, key.Kid, key.Alg, key.N, key.E)
        ]);

        return Task.FromResult<Result<GetJwksResponse>>(response);
    }
}
