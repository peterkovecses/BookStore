namespace BookStore.Common.Validation;

public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validationMetadata = await GetAggregatedValidationErrors(request, cancellationToken);

        if (validationMetadata.Count == 0)
        {
            return await next(cancellationToken);
        }

        return CreateAggregatedFailureResponse(validationMetadata);
    }

    private async Task<Dictionary<string, object>> GetAggregatedValidationErrors(TRequest request, CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, object>();

        foreach (var validator in validators)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    var propertyName = error.PropertyName;

                    if (!metadata.TryGetValue(propertyName, out var existingValue))
                    {
                        metadata[propertyName] = new List<string> { error.ErrorMessage };

                        continue;
                    }

                    if (existingValue is List<string> messages)
                    {
                        messages.Add(error.ErrorMessage);
                    }
                }
            }
        }

        return metadata;
    }

    private static TResponse CreateAggregatedFailureResponse(Dictionary<string, object> metadata)
    {
        var error = Error.Validation(
            ErrorCodes.Validation,
            "Validation failed.",
            metadata);

        return Result.CreateFailure<TResponse>([error]);
    }
}
