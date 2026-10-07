using auth.Domain;
using MediatR;

namespace auth.Application.Queries.Auth;

/// <summary>
/// Checks whether a login is already registered.
/// </summary>
public sealed record CheckLoginExistsQuery(string Login)
    : IRequest<Result<CheckLoginExistsResponse>>;

/// <summary>
/// Indicates whether a login is already registered.
/// </summary>
public sealed record CheckLoginExistsResponse(bool Exists);
