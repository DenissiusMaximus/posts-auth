using auth.Application.Commands.ApiKeys;
using FluentValidation;

namespace auth.Application.Validators.ApiKeys;

public class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
