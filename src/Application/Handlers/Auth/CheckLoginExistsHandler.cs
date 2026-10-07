using auth.Application.Queries.Auth;
using auth.Domain;
using auth.Domain.Repositories;
using MediatR;

namespace auth.Application.Handlers.Auth;

public sealed class CheckLoginExistsHandler(IUserRepository userRepository)
    : IRequestHandler<CheckLoginExistsQuery, Result<CheckLoginExistsResponse>>
{
    public async Task<Result<CheckLoginExistsResponse>> Handle(
        CheckLoginExistsQuery request,
        CancellationToken cancellationToken)
    {
        var login = request.Login.Trim();
        var user = await userRepository.GetByLoginAsync(login, cancellationToken);

        return new CheckLoginExistsResponse(user is not null);
    }
}
