using auth.Domain;
using auth.Domain.Errors;
using FluentValidation;
using MediatR;


namespace auth.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .Where(r => r.Errors.Count != 0)
                .SelectMany(r => r.Errors)
                .Select(f => new Error(f.PropertyName, f.ErrorMessage, ErrorType.Validation))
                .ToArray();

            if (failures.Length != 0)
            {
                return CreateValidationResult<TResponse>(failures);
            }
        }

        return await next();
    }

    private static TResult CreateValidationResult<TResult>(Error[] errors)
    {
        var validationError = new ValidationError(errors);

        if (typeof(TResult) == typeof(Result))
        {
            return (TResult)(object)Result.Failure(validationError);
        }

        if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var failureMethod = typeof(Result).GetMethods().First(m => m.Name == "Failure" && m.IsGenericMethod);
            var genericMethod = failureMethod.MakeGenericMethod(resultType);
            return (TResult)genericMethod.Invoke(null, new object?[] { validationError })!;
        }

        throw new ValidationException(errors.Select(e => new FluentValidation.Results.ValidationFailure(e.Code, e.Message)));
    }
}

