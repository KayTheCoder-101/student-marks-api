using MarksApi.Dtos;
using MarksApi.Infrastructure;
using MarksApi.Services;

namespace MarksApi.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/courses/{courseId:int}/stats", async (int courseId, IReportService reports, CancellationToken ct) =>
                Results.Ok(await reports.GetCourseStatsAsync(courseId, ct)))
            .RequireAuthorization(Policies.TeacherOnly)
            .WithTags("Reports")
            .WithSummary("Course statistics (graded enrollments only)")
            .Produces<CourseStatsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/students/{id:int}/transcript", async (int id, IReportService reports, CancellationToken ct) =>
                Results.Ok(await reports.GetTranscriptAsync(id, ct)))
            .RequireAuthorization(Policies.AnyUser)
            .WithTags("Reports")
            .WithSummary("Transcript with credit-hour weighted average (Teacher, or the student himself or herself)")
            .Produces<TranscriptResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
