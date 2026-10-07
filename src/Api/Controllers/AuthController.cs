using auth.Api.Common;
using auth.Application.Commands.Auth;
using auth.Application.Queries.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace auth.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(ISender sender) : BaseApiController
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        CancellationToken ct)
        => HandleResult(await sender.Send(command, ct));

    [HttpGet("login/exists")]
    public async Task<IActionResult> LoginExists(
        [FromQuery] string login,
        CancellationToken ct)
        => HandleResult(await sender.Send(new CheckLoginExistsQuery(login), ct));

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken ct)
        => HandleResult(await sender.Send(command, ct));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshCommand command,
        CancellationToken ct)
        => HandleResult(await sender.Send(command, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutCommand command,
        CancellationToken ct)
        => HandleResult(await sender.Send(command, ct));

    [HttpPost("token")]
    public async Task<IActionResult> ExchangeApiKey(
        [FromBody] ExchangeApiKeyCommand command,
        CancellationToken ct)
        => HandleResult(await sender.Send(command, ct));
}
