using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Snakk.Api.Filters;

public class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var requestObject = context.Arguments.OfType<T>().FirstOrDefault();

        if (requestObject is null)
            return await next(context);

        var validationResult = await validator.ValidateAsync(requestObject);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}
