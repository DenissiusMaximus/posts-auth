using auth.Application.Abstractions;

namespace auth.Api.Common;

public sealed class CurrentUser : ICurrentUser
{
    public Guid? UserId { get; private set; }

    public void SetUser(Guid userId) => UserId = userId;
}
