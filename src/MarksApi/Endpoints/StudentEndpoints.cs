using MarksApi.Dtos;
using MarksApi.Infrastructure;
using MarksApi.Services;

namespace MarksApi.Endpoints;

public static class StudentEndpoints
{
    public static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/students").WithTags("Students");

        group.MapGet("/", async ([AsParameters] PageQuery query, IStudentService students, CancellationToken ct) =>
                Results.Ok(await students.ListAsync(query, ct)))
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("List students (paged, searchable, sortable)")
            .Produces<PagedResult<StudentResponse>>();

        group.MapGet("/{id:int}", async (int id, IStudentService students, CancellationToken ct) =>
                Results.Ok(await students.GetAsync(id, ct)))
            .RequireAuthorization(Policies.AnyUser)
            .WithSummary("Get a student (Teacher, or the student himself or herself)")
            .Produces<StudentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateStudentRequest request, IStudentService students, CancellationToken ct) =>
            {
                var created = await students.CreateAsync(request, ct);
                return Results.Created($"/api/students/{created.Id}", created);
            })
            .AddEndpointFilter<ValidationFilter<CreateStudentRequest>>()
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("Create a student")
            .Produces<StudentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:int}", async (int id, UpdateStudentRequest request, IStudentService students, CancellationToken ct) =>
                Results.Ok(await students.UpdateAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateStudentRequest>>()
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("Update a student")
            .Produces<StudentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:int}", async (int id, IStudentService students, CancellationToken ct) =>
            {
                await students.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("Delete a student (409 if the student still has enrollments)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
