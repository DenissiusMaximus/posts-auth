using auth.Application.Commands.Owner;
using FluentValidation;

namespace auth.Application.Validators.Owner;

public class OwnerSeedCommandValidator : AbstractValidator<OwnerSeedCommand>
{
    public OwnerSeedCommandValidator()
    {
        RuleFor(x => x.Login).NotEmpty().MinimumLength(3).MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(100);
    }
}
