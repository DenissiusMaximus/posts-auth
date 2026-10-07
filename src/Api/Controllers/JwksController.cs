using auth.Api.Common;
using auth.Application.Queries.Jwks;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace auth.Api.Controllers;

[ApiController]
[Route("")]
public sealed class JwksController(ISender sender) : BaseApiController
{
    [HttpGet(".well-known/jwks")]
    public async Task<IActionResult> Get(CancellationToken ct)
        => HandleResult(await sender.Send(new GetJwksQuery(), ct));
}
