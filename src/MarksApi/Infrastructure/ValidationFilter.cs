using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MarksApi.Infrastructure;

/// <summary>Validates the request DTO (data annotations) at the edge and returns a 400 problem details with an errors object.</summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["body"] = ["A request body is required."] },
                title: "One or more validation errors occurred.");
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : ["body"])
                    .Select(m => (Key: JsonNamingPolicy.CamelCase.ConvertName(m), Message: r.ErrorMessage ?? "The value is not valid.")))
                .GroupBy(x => x.Key)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Message).Distinct().ToArray());

            return Results.ValidationProblem(errors, title: "One or more validation errors occurred.");
        }

        return await next(context);
    }
}
