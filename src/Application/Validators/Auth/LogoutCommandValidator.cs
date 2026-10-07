using auth.Application.Commands.Auth;
using FluentValidation;

namespace auth.Application.Validators.Auth;

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
