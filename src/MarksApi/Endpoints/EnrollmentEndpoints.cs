using MarksApi.Dtos;
using MarksApi.Infrastructure;
using MarksApi.Services;

namespace MarksApi.Endpoints;

public static class EnrollmentEndpoints
{
    public static IEndpointRouteBuilder MapEnrollmentEndpoints(this IEndpointRouteBuilder app)
    {
        var courses = app.MapGroup("/api/courses").WithTags("Enrollments and Marks");

        courses.MapPost("/{courseId:int}/enrollments", async (int courseId, EnrollStudentRequest request, IEnrollmentService enrollments, CancellationToken ct) =>
            {
                var created = await enrollments.EnrollAsync(courseId, request, ct);
                return Results.Created($"/api/enrollments/{created.Id}", created);
            })
            .AddEndpointFilter<ValidationFilter<EnrollStudentRequest>>()
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("Enroll a student in a course")
            .Produces<EnrollmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        courses.MapGet("/{courseId:int}/enrollments", async (int courseId, [AsParameters] PageQuery query, IEnrollmentService enrollments, CancellationToken ct) =>
                Results.Ok(await enrollments.ListForCourseAsync(courseId, query, ct)))
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("List the enrollments of a course (paged, searchable, sortable)")
            .Produces<PagedResult<CourseEnrollmentItem>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var group = app.MapGroup("/api/enrollments").WithTags("Enrollments and Marks");

        // Extra endpoint (not in the specification): gives the Location header of a new enrollment a real target.
        group.MapGet("/{id:int}", async (int id, IEnrollmentService enrollments, CancellationToken ct) =>
                Results.Ok(await enrollments.GetAsync(id, ct)))
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("Get one enrollment")
            .Produces<EnrollmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:int}/mark", async (int id, UpdateMarkRequest request, IEnrollmentService enrollments, CancellationToken ct) =>
                Results.Ok(await enrollments.SetMarkAsync(id, request, ct)))
            .AddEndpointFilter<ValidationFilter<UpdateMarkRequest>>()
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("Record a mark (0 to 100)")
            .Produces<EnrollmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:int}", async (int id, IEnrollmentService enrollments, CancellationToken ct) =>
            {
                await enrollments.DeleteAsync(id, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Policies.TeacherOnly)
            .WithSummary("Remove an enrollment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
