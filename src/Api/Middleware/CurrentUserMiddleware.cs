using System.IdentityModel.Tokens.Jwt;
using auth.Api.Common;
using Microsoft.AspNetCore.Http;

namespace auth.Api.Middleware;

public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, CurrentUser currentUser)
    {
        var subject = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (Guid.TryParse(subject, out var userId) && userId != Guid.Empty)
            currentUser.SetUser(userId);

        await next(httpContext);
    }
}
