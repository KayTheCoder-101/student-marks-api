using MarksApi.Dtos;
using MarksApi.Infrastructure;
using MarksApi.Services;

namespace MarksApi.Endpoints;

public static class CourseEndpoints
{
    public static IEndpointRouteBuilder MapCourseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/courses").WithTags("Courses");

        group.MapGet("/", async ([AsParameters] PageQuery query, ICourseService courses, CancellationToken ct) =>
                Results.Ok(await courses.ListAsync(query, ct)))
            .RequireAuthorization(Policies.AnyUser)
            .WithSummary("List courses (paged, searchable, sortable)")
            .Produces<PagedResult<CourseResponse>>();

        group.MapGet("/{id:int}", async (int id, ICourseService courses, CancellationToken ct) =>
                Results.Ok(await courses.GetAsync(id, ct)))
            .RequireAuthorization(Policies.AnyUser)
            .WithSummary("Get a course")
            .Produces<CourseResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateCourseRequest request, ICourseService courses, CancellationToken ct) =>
            {
                var created = await courses.CreateAsync(request, ct);
                return Results.Created($"/api/courses/{created.Id}", created);
            })
            .AddEndpointFilter<ValidationFilter<CreateCourseRequest>>()
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("Create a course")
            .Produces<CourseResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:int}", async (int id, UpdateCourseRequest request, ICourseService courses, CancellationToken ct) =>
                Results.Ok(await courses.UpdateAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateCourseRequest>>()
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("Update a course")
            .Produces<CourseResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:int}", async (int id, ICourseService courses, CancellationToken ct) =>
            {
                await courses.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("Delete a course (409 if it still has enrollments)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
