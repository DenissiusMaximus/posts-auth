using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using auth.Domain;
using auth.Domain.Errors;

namespace auth.Api.Common;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
            return Ok();

        return HandleFailure(result.Error);
    }

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return HandleFailure(result.Error);
    }

    private IActionResult HandleFailure(Error error)
    {
        var (status, title, type) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found", "https://httpstatuses.com/404"),
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Validation Error", "https://httpstatuses.com/400"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict", "https://httpstatuses.com/409"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized", "https://httpstatuses.com/401"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden", "https://httpstatuses.com/403"),
            _ => (StatusCodes.Status400BadRequest, "Bad Request", "https://httpstatuses.com/400")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = error.Message
        };

        problem.Extensions["code"] = error.Code;

        if (error is ValidationError validationError)
        {
            problem.Extensions["errors"] = validationError.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
        }

        return StatusCode(status, problem);
    }
}
