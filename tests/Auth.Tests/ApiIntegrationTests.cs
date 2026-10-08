using System.Security.Cryptography;
using auth.Application.Commands.Auth;
using auth.Application.Queries.Auth;
using auth.Domain;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Auth.Tests;

public sealed class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Register_endpoint_returns_json_login_response()
    {
        using var response = await _client.PostAsJsonAsync(
            "/register", new { login = "alice", password = "secret1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal("access-token", body!.AccessToken);
        Assert.Equal("refresh-token", body.RefreshToken);
    }

    [Fact]
    public async Task Login_exists_endpoint_returns_result_from_mediator()
    {
        using var response = await _client.GetAsync("/login/exists?login=alice");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckLoginExistsResponse>();
        Assert.True(body!.Exists);
    }

    [Fact]
    public async Task Paged_api_keys_endpoint_is_protected()
    {
        using var response = await _client.GetAsync("/keys/paged?page=0&pageSize=101");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_api_key_endpoint_requires_authentication()
    {
        using var response = await _client.GetAsync("/keys");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_route_returns_not_found()
    {
        using var response = await _client.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory()
    {
        using var rsa = RSA.Create(2048);
        Environment.SetEnvironmentVariable("Jwt__PrivateKeyPem", rsa.ExportPkcs8PrivateKeyPem());
        Environment.SetEnvironmentVariable("DB_HOST", "localhost");
        Environment.SetEnvironmentVariable("DB_PORT", "5432");
        Environment.SetEnvironmentVariable("DB_NAME", "tests");
        Environment.SetEnvironmentVariable("DB_USER", "tests");
        Environment.SetEnvironmentVariable("DB_PASSWORD", "tests");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISender>();
            services.AddSingleton<ISender, IntegrationSender>();
        });
    }
}

internal sealed class IntegrationSender : ISender
{
    public Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        object response = request switch
        {
            RegisterCommand => Result.Success(new LoginResponse("access-token", "refresh-token")),
            CheckLoginExistsQuery => Result.Success(new CheckLoginExistsResponse(true)),
            _ => throw new InvalidOperationException($"Unexpected request: {request.GetType().Name}")
        };
        return Task.FromResult((TResponse)response);
    }

    public Task<TResponse> Send<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
        => Send((IRequest<TResponse>)request, cancellationToken);

    public Task Send<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest
        => throw new NotSupportedException();

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(
        object request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
