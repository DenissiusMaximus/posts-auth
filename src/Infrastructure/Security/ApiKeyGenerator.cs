using System.Security.Cryptography;
using System.Text;
using auth.Application.Abstractions;

namespace auth.Infrastructure.Security;

public sealed class ApiKeyGenerator : IApiKeyGenerator
{
    private const string KeyPrefix = "ak";
    private const int SecretSizeBytes = 32;

    public GeneratedApiKey Generate(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("API key ID must not be empty.", nameof(id));

        var secret = Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretSizeBytes));
        var key = BuildKey(id, secret);

        return new GeneratedApiKey(
            Id: id,
            Key: key,
            Prefix: $"{KeyPrefix}_{id:N}",
            SecretHash: HashSecret(secret),
            Last4: secret[^4..]);
    }

    public bool Verify(Guid id, string apiKey, string secretHash)
    {
        if (id == Guid.Empty
            || string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(secretHash))
        {
            return false;
        }

        var parts = apiKey.Split('_', 3, StringSplitOptions.None);
        if (parts.Length != 3
            || !string.Equals(parts[0], KeyPrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(parts[1], "N", out var keyId)
            || keyId != id
            || string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        var actualHash = Convert.FromHexString(HashSecret(parts[2]));
        byte[] expectedHash;

        try
        {
            expectedHash = Convert.FromHexString(secretHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static string BuildKey(Guid id, string secret)
        => $"{KeyPrefix}_{id:N}_{secret}";

    private static string HashSecret(string secret)
        => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
