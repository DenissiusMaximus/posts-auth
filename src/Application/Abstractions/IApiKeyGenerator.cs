namespace auth.Application.Abstractions;

public interface IApiKeyGenerator
{
    GeneratedApiKey Generate(Guid id);

    bool Verify(Guid id, string apiKey, string secretHash);
}

public sealed record GeneratedApiKey(
    Guid Id,
    string Key,
    string Prefix,
    string SecretHash,
    string Last4);
