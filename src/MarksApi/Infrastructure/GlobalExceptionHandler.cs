using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MarksApi.Infrastructure;

/// <summary>Turns every unhandled exception into a problem details response. Never leaks a stack trace.</summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        switch (exception)
        {
            case RequestValidationException validation:
                problem = new HttpValidationProblemDetails(validation.Errors)
                {
                    Status = validation.StatusCode,
                    Title = validation.Title
                };
                break;

            case AppException app:
                logger.LogInformation("Request rejected: {Title} ({Status})", app.Title, app.StatusCode);
                problem = new ProblemDetails { Status = app.StatusCode, Title = app.Title, Detail = app.Detail };
                break;

            case BadHttpRequestException:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad request",
                    Detail = "The request could not be read. Check the JSON body and the parameter types."
                };
                break;

            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Detail = "Please try again later."
                };
                break;
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
