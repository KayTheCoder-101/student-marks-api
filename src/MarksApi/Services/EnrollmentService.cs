using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public interface IEnrollmentService
{
    Task<EnrollmentResponse> EnrollAsync(int courseId, EnrollStudentRequest request, CancellationToken ct);
    Task<PagedResult<CourseEnrollmentItem>> ListForCourseAsync(int courseId, PageQuery query, CancellationToken ct);
    Task<EnrollmentResponse> GetAsync(int id, CancellationToken ct);
    Task<EnrollmentResponse> SetMarkAsync(int id, UpdateMarkRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class EnrollmentService(AppDbContext db, ICurrentUser currentUser, ILogger<EnrollmentService> logger) : IEnrollmentService
{
    private static readonly string[] SortFields = ["id", "fullname", "registrationno", "mark", "enrolledatutc"];

    public async Task<EnrollmentResponse> EnrollAsync(int courseId, EnrollStudentRequest request, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == courseId, ct))
            throw new NotFoundException("Course not found", $"No course with id {courseId}.");
        if (!await db.Students.AnyAsync(s => s.Id == request.StudentId, ct))
            throw new NotFoundException("Student not found", $"No student with id {request.StudentId}.");

        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAtUtc = DateTime.UtcNow
        };

        db.Enrollments.Add(enrollment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // The unique index (student_id, course_id) decides, so two simultaneous requests cannot both win.
            logger.LogWarning("Enrollment rejected: student {StudentId} already in course {CourseId}", request.StudentId, courseId);
            throw new ConflictException("Duplicate enrollment", "The student is already enrolled in this course.");
        }
        catch (DbUpdateException ex) when (DbErrors.IsForeignKeyViolation(ex))
        {
            throw new NotFoundException("Student or course not found", "The student or the course was deleted while enrolling.");
        }

        logger.LogInformation("Student {StudentId} enrolled in course {CourseId} (enrollment {EnrollmentId}) by {User}",
            request.StudentId, courseId, enrollment.Id, currentUser.Username);
        return ToResponse(enrollment);
    }

    public async Task<PagedResult<CourseEnrollmentItem>> ListForCourseAsync(int courseId, PageQuery q, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == courseId, ct))
            throw new NotFoundException("Course not found", $"No course with id {courseId}.");

        var (page, pageSize) = Paging.Normalize(q.Page, q.PageSize);
        var (field, desc) = Paging.ParseSort(q.Sort, "fullname", SortFields);

        var query = db.Enrollments.AsNoTracking().Where(e => e.CourseId == courseId);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var pattern = $"%{Paging.EscapeLike(q.Search.Trim())}%";
            query = query.Where(e => EF.Functions.ILike(e.Student.FullName, pattern)
                                  || EF.Functions.ILike(e.Student.RegistrationNo, pattern));
        }

        query = Sort(query, field, desc);

        return await query
            .Select(e => new CourseEnrollmentItem(e.Id, e.StudentId, e.Student.RegistrationNo, e.Student.FullName, e.Mark, e.EnrolledAtUtc))
            .ToPagedAsync(page, pageSize, ct);
    }

    public async Task<EnrollmentResponse> GetAsync(int id, CancellationToken ct) =>
        await db.Enrollments.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EnrollmentResponse(e.Id, e.StudentId, e.CourseId, e.Mark, e.EnrolledAtUtc, e.Version))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Enrollment not found", $"No enrollment with id {id}.");

    public async Task<EnrollmentResponse> SetMarkAsync(int id, UpdateMarkRequest request, CancellationToken ct)
    {
        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Enrollment not found", $"No enrollment with id {id}.");

        // Optimistic concurrency: compare against the version the client last saw (if it sent one).
        if (request.Version is not null)
            db.Entry(enrollment).Property(e => e.Version).OriginalValue = request.Version.Value;

        var oldMark = enrollment.Mark;
        enrollment.Mark = request.Mark;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Mark update for enrollment {EnrollmentId} rejected: concurrent change", id);
            throw new ConflictException("Concurrent update", "The enrollment was changed by someone else. Reload it and try again.");
        }

        logger.LogInformation("Mark for enrollment {EnrollmentId} changed from {OldMark} to {NewMark} by {User}",
            id, oldMark, enrollment.Mark, currentUser.Username);
        return ToResponse(enrollment);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var deleted = await db.Enrollments.Where(e => e.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new NotFoundException("Enrollment not found", $"No enrollment with id {id}.");
        logger.LogInformation("Enrollment {EnrollmentId} deleted by {User}", id, currentUser.Username);
    }

    private static EnrollmentResponse ToResponse(Enrollment e) => new(e.Id, e.StudentId, e.CourseId, e.Mark, e.EnrolledAtUtc, e.Version);

    private static IQueryable<Enrollment> Sort(IQueryable<Enrollment> q, string field, bool desc) => field switch
    {
        "registrationno" => desc ? q.OrderByDescending(e => e.Student.RegistrationNo).ThenBy(e => e.Id) : q.OrderBy(e => e.Student.RegistrationNo).ThenBy(e => e.Id),
        "mark" => desc ? q.OrderByDescending(e => e.Mark).ThenBy(e => e.Id) : q.OrderBy(e => e.Mark).ThenBy(e => e.Id),
        "enrolledatutc" => desc ? q.OrderByDescending(e => e.EnrolledAtUtc).ThenBy(e => e.Id) : q.OrderBy(e => e.EnrolledAtUtc).ThenBy(e => e.Id),
        "id" => desc ? q.OrderByDescending(e => e.Id) : q.OrderBy(e => e.Id),
        _ => desc ? q.OrderByDescending(e => e.Student.FullName).ThenBy(e => e.Id) : q.OrderBy(e => e.Student.FullName).ThenBy(e => e.Id)
    };
}
