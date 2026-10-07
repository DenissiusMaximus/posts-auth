using auth.Application.Queries.Auth;
using FluentValidation;

namespace auth.Application.Validators.Auth;

public sealed class CheckLoginExistsQueryValidator : AbstractValidator<CheckLoginExistsQuery>
{
    public CheckLoginExistsQueryValidator()
    {
        RuleFor(x => x.Login)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(50);
    }
}
