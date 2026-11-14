using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MarksApi.Services;

public interface IReportService
{
    Task<CourseStatsResponse> GetCourseStatsAsync(int courseId, CancellationToken ct);
    Task<TranscriptResponse> GetTranscriptAsync(int studentId, CancellationToken ct);
}

public sealed class ReportService(AppDbContext db, ICurrentUser currentUser, IOptions<GradingOptions> grading) : IReportService
{
    public async Task<CourseStatsResponse> GetCourseStatsAsync(int courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new { c.Id, c.Code })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Course not found", $"No course with id {courseId}.");

        var marks = await db.Enrollments.AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => e.Mark)
            .ToListAsync(ct);

        var s = GradeCalculator.Summarize(marks, grading.Value.PassMark);
        return new CourseStatsResponse(course.Id, course.Code, s.Enrolled, s.Graded, s.Average, s.Highest, s.Lowest, s.Passed);
    }

    public async Task<TranscriptResponse> GetTranscriptAsync(int studentId, CancellationToken ct)
    {
        currentUser.EnsureCanAccessStudent(studentId);

        var student = await db.Students.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new { s.Id, s.RegistrationNo, s.FullName })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Student not found", $"No student with id {studentId}.");

        var rows = await db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .OrderBy(e => e.Course.Code)
            .Select(e => new { e.Course.Code, e.Course.CreditHours, e.Mark })
            .ToListAsync(ct);

        var passMark = grading.Value.PassMark;
        var courses = rows
            .Select(r => new TranscriptCourse(r.Code, r.CreditHours, r.Mark, GradeCalculator.IsPassed(r.Mark, passMark)))
            .ToList();

        var weighted = GradeCalculator.WeightedAverage(rows.Select(r => (r.Mark, r.CreditHours)));

        return new TranscriptResponse(student.Id, student.RegistrationNo, student.FullName, courses, weighted);
    }
}
