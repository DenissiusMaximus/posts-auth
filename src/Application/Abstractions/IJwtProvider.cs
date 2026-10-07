using auth.Domain;

namespace auth.Application.Abstractions;

public interface IJwtProvider
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(Guid subjectId);

    (string Token, string Jti, DateTime ExpiresAtUtc) GenerateRefreshToken(Guid subjectId);

    Result<JwtValidationResult> ValidateRefreshToken(string token);

    string GetPublicKeyPem();

    PublicJwk GetPublicJwk();
}

public sealed record JwtValidationResult(Guid SubjectId, string Jti, DateTime ExpiresAtUtc);

public sealed record PublicJwk(
    string Kty,
    string Use,
    string Kid,
    string Alg,
    string N,
    string E);
