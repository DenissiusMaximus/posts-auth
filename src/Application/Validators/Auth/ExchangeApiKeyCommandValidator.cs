using auth.Application.Commands.Auth;
using FluentValidation;

namespace auth.Application.Validators.Auth;

public class ExchangeApiKeyCommandValidator : AbstractValidator<ExchangeApiKeyCommand>
{
    public ExchangeApiKeyCommandValidator()
    {
        RuleFor(x => x.ApiKey).NotEmpty();
    }
}
