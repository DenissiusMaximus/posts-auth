namespace auth.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
}
