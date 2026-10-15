using auth.Api.Middleware;
using auth.Api.Common;
using auth.Application.Abstractions;
using auth.Application.Behaviors;
using auth.Infrastructure;
using auth.Infrastructure.Persistence;
using auth.Infrastructure.Security;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<JwtProvider>();
builder.Services.AddSingleton<IJwtProvider>(sp => sp.GetRequiredService<JwtProvider>());
builder.Services.AddSingleton<IApiKeyGenerator, ApiKeyGenerator>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ICurrentUser>(
    serviceProvider => serviceProvider.GetRequiredService<CurrentUser>());
builder.Services.AddInfrastructure();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();

builder.Services.AddOptions<JwtBearerOptions>()
    .Configure<JwtProvider>((options, jwtProvider) =>
    {
        options.MapInboundClaims = false;
        var validationParameters = jwtProvider.CreateAccessTokenValidationParameters();
        validationParameters.ValidateIssuer = false;
        validationParameters.ValidateAudience = false;
        validationParameters.ValidateLifetime = false;
        options.TokenValidationParameters = validationParameters;
    });

builder.Services.AddControllers();

builder.Services.AddValidatorsFromAssembly(typeof(auth.Application.Handlers.Auth.LoginHandler).Assembly);
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(auth.Application.Handlers.Auth.LoginHandler).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.Logger.LogInformation("Starting Auth Service [Environment: {Environment}]", app.Environment.EnvironmentName);

if (!app.Environment.IsEnvironment("Testing"))
{
    app.Logger.LogInformation("Applying database migrations...");
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations applied successfully.");
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    app.Logger.LogInformation("Auth Service started successfully and is ready to accept requests.");
});
app.Lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Auth Service is stopping...");
});
app.Lifetime.ApplicationStopped.Register(() =>
{
    app.Logger.LogInformation("Auth Service stopped.");
});

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseAuthorization();

app.MapControllers();


app.Run();

public partial class Program { }
