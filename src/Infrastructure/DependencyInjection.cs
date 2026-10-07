using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using auth.Application.Abstractions;
using auth.Domain.Repositories;
using auth.Infrastructure.Persistence;
using auth.Infrastructure.Persistence.Repositories;
using auth.Infrastructure.Security;

namespace auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(BuildConnectionStringFromEnvironment()));

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IApiKeyPermissionRepository, ApiKeyPermissionRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        return services;
    }

    private static string BuildConnectionStringFromEnvironment()
    {
        var portValue = GetRequiredEnvironmentVariable("DB_PORT");
        if (!int.TryParse(portValue, out var port))
        {
            throw new InvalidOperationException("DB_PORT must be a valid integer.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = GetRequiredEnvironmentVariable("DB_HOST"),
            Port = port,
            Database = GetRequiredEnvironmentVariable("DB_NAME"),
            Username = GetRequiredEnvironmentVariable("DB_USER"),
            Password = GetRequiredEnvironmentVariable("DB_PASSWORD")
        }.ConnectionString;
    }

    private static string GetRequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException(
            $"Required environment variable '{name}' is not configured.");
}
