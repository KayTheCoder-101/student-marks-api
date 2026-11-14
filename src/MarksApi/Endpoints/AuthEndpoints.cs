using MarksApi.Dtos;
using MarksApi.Infrastructure;
using MarksApi.Services;

namespace MarksApi.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
                Results.Ok(await auth.LoginAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .AllowAnonymous()
            .RequireRateLimiting("login")
            .WithSummary("Log in and receive a JWT")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
