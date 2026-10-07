using auth.Application.Commands.ApiKeys;
using FluentValidation;

namespace auth.Application.Validators.ApiKeys;

public class RevokeApiKeyCommandValidator : AbstractValidator<RevokeApiKeyCommand>
{
    public RevokeApiKeyCommandValidator()
    {
        RuleFor(x => x.KeyId).NotEmpty();
    }
}
