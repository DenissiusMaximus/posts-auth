using auth.Api.Middleware;
using auth.Api.Common;
using auth.Application.Abstractions;
using auth.Application.Behaviors;
using auth.Infrastructure;
using auth.Infrastructure.Security;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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
        options.TokenValidationParameters = jwtProvider.CreateAccessTokenValidationParameters();
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

app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseMiddleware<CurrentUserMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => "Ok");

app.Run();

