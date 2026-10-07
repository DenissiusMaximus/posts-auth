using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using auth.Application.Abstractions;
using auth.Domain;
using auth.Domain.Errors;
using Microsoft.IdentityModel.Tokens;

namespace auth.Infrastructure.Security;

public sealed class JwtProvider : IJwtProvider, IDisposable
{
    private const int MinRsaKeySizeBits = 2048;
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly TimeSpan _accessTokenLifetime;
    private readonly TimeSpan _refreshTokenLifetime;

    public JwtProvider()
    {
        var privateKeyPem = Environment.GetEnvironmentVariable("Jwt__PrivateKeyPem");

        if (string.IsNullOrWhiteSpace(privateKeyPem))
            throw new InvalidOperationException(
                "RSA private key is not configured. Set the Jwt__PrivateKeyPem environment variable.");

        _accessTokenLifetime = ReadLifetime("Jwt__AccessTokenExpiration", TimeSpan.FromMinutes(15));
        _refreshTokenLifetime = ReadLifetime("Jwt__RefreshTokenExpiration", TimeSpan.FromDays(30));

        _rsa = RSA.Create();
        _rsa.ImportFromPem(privateKeyPem.Replace("\\n", "\n"));

        if (_rsa.KeySize < MinRsaKeySizeBits)
            throw new InvalidOperationException($"RSA private key must be at least {MinRsaKeySizeBits} bits.");

        _signingKey = new RsaSecurityKey(_rsa)
        {
            KeyId = Base64UrlEncoder.Encode(_rsa.ExportParameters(false).Modulus!)
        };
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(Guid subjectId)
    {
        var expiresAtUtc = DateTime.UtcNow.Add(_accessTokenLifetime);
        var token = GenerateToken(subjectId, Guid.NewGuid().ToString("N"), expiresAtUtc);

        return (token, expiresAtUtc);
    }

    public (string Token, string Jti, DateTime ExpiresAtUtc) GenerateRefreshToken(Guid subjectId)
    {
        var jti = Guid.NewGuid().ToString("N");
        var expiresAtUtc = DateTime.UtcNow.Add(_refreshTokenLifetime);
        var token = GenerateToken(subjectId, jti, expiresAtUtc);

        return (token, jti, expiresAtUtc);
    }

    public Result<JwtValidationResult> ValidateRefreshToken(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, parameters, out var validatedToken);
            var subjectClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub);
            var jtiClaim = principal.FindFirst(JwtRegisteredClaimNames.Jti);

            if (subjectClaim is not null
                && Guid.TryParse(subjectClaim.Value, out var subjectId)
                && subjectId != Guid.Empty
                && jtiClaim is not null
                && !string.IsNullOrWhiteSpace(jtiClaim.Value))
            {
                return new JwtValidationResult(subjectId, jtiClaim.Value, validatedToken.ValidTo);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (SecurityTokenException)
        {
        }

        return new Error("Tokens.RefreshTokenInvalid", "The provided refresh token is invalid or expired.", ErrorType.Unauthorized);
    }

    public string GetPublicKeyPem() => _rsa.ExportSubjectPublicKeyInfoPem();

    public TokenValidationParameters CreateAccessTokenValidationParameters() =>
        new()
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub
        };

    public PublicJwk GetPublicJwk()
    {
        var parameters = _rsa.ExportParameters(false);

        return new PublicJwk(
            Kty: "RSA",
            Use: "sig",
            Kid: _signingKey.KeyId!,
            Alg: SecurityAlgorithms.RsaSha256,
            N: Base64UrlEncoder.Encode(parameters.Modulus!),
            E: Base64UrlEncoder.Encode(parameters.Exponent!));
    }

    public void Dispose() => _rsa.Dispose();

    private string GenerateToken(Guid subjectId, string jti, DateTime expiresAtUtc)
    {
        if (subjectId == Guid.Empty)
            throw new ArgumentException("Subject ID must not be empty.", nameof(subjectId));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, subjectId.ToString("D")),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static TimeSpan ReadLifetime(string variableName, TimeSpan fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : TimeSpan.Parse(value);
    }
}
