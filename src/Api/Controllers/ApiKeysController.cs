using auth.Api.Common;
using auth.Application.Commands.ApiKeys;
using auth.Application.Queries.ApiKeys;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace auth.Api.Controllers;

[ApiController]
[Route("auth/keys")]
[Authorize]
public sealed class ApiKeysController(ISender sender) : BaseApiController
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => HandleResult(await sender.Send(new ListApiKeysQuery(), ct));

    [HttpGet("permissions")]
    public async Task<IActionResult> ListPermissions(CancellationToken ct)
        => HandleResult(await sender.Send(new ListApiKeyPermissionsQuery(), ct));

    [HttpGet("paged")]
    public async Task<IActionResult> ListPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        if (page < 1)
            return BadRequest("The page must be greater than zero.");

        if (pageSize < 1 || pageSize > MaxPageSize)
            return BadRequest($"The pageSize must be between 1 and {MaxPageSize}.");

        return HandleResult(await sender.Send(
            new ListApiKeysPagedQuery(page, pageSize),
            ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateApiKeyCommand command,
        CancellationToken ct)
        => HandleResult(await sender.Send(command, ct));

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(
        Guid id,
        CancellationToken ct)
        => HandleResult(await sender.Send(new RevokeApiKeyCommand(id), ct));
}
