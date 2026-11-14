using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public interface ICourseService
{
    Task<PagedResult<CourseResponse>> ListAsync(PageQuery query, CancellationToken ct);
    Task<CourseResponse> GetAsync(int id, CancellationToken ct);
    Task<CourseResponse> CreateAsync(CreateCourseRequest request, CancellationToken ct);
    Task<CourseResponse> UpdateAsync(int id, UpdateCourseRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class CourseService(AppDbContext db, ICurrentUser currentUser, ILogger<CourseService> logger) : ICourseService
{
    private static readonly string[] SortFields = ["id", "code", "title", "credithours", "createdatutc"];

    private const string DuplicateTitle = "Duplicate course";
    private const string DuplicateDetail = "A course with this code already exists.";

    public async Task<PagedResult<CourseResponse>> ListAsync(PageQuery q, CancellationToken ct)
    {
        var (page, pageSize) = Paging.Normalize(q.Page, q.PageSize);
        var (field, desc) = Paging.ParseSort(q.Sort, "code", SortFields);

        IQueryable<Course> query = db.Courses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var pattern = $"%{Paging.EscapeLike(q.Search.Trim())}%";
            query = query.Where(c => EF.Functions.ILike(c.Code, pattern) || EF.Functions.ILike(c.Title, pattern));
        }

        query = Sort(query, field, desc);

        return await query
            .Select(c => new CourseResponse(c.Id, c.Code, c.Title, c.CreditHours, c.CreatedAtUtc))
            .ToPagedAsync(page, pageSize, ct);
    }

    public async Task<CourseResponse> GetAsync(int id, CancellationToken ct) =>
        await db.Courses.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponse(c.Id, c.Code, c.Title, c.CreditHours, c.CreatedAtUtc))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Course not found", $"No course with id {id}.");

    public async Task<CourseResponse> CreateAsync(CreateCourseRequest request, CancellationToken ct)
    {
        var course = new Course
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            CreditHours = request.CreditHours,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Courses.Add(course);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            logger.LogWarning("Create course rejected: duplicate code {Code}", course.Code);
            throw new ConflictException(DuplicateTitle, DuplicateDetail);
        }

        logger.LogInformation("Course {CourseId} created ({Code}) by {User}", course.Id, course.Code, currentUser.Username);
        return ToResponse(course);
    }

    public async Task<CourseResponse> UpdateAsync(int id, UpdateCourseRequest request, CancellationToken ct)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Course not found", $"No course with id {id}.");

        course.Code = request.Code.Trim().ToUpperInvariant();
        course.Title = request.Title.Trim();
        course.CreditHours = request.CreditHours;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            logger.LogWarning("Update of course {CourseId} rejected: duplicate code", id);
            throw new ConflictException(DuplicateTitle, DuplicateDetail);
        }

        logger.LogInformation("Course {CourseId} updated by {User}", id, currentUser.Username);
        return ToResponse(course);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        int deleted;
        try
        {
            deleted = await db.Courses.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
        }
        catch (Exception ex) when (DbErrors.IsStillReferenced(ex))
        {
            logger.LogWarning("Delete of course {CourseId} rejected: still has enrollments", id);
            throw new ConflictException("Course cannot be deleted", "The course still has enrollments.");
        }

        if (deleted == 0) throw new NotFoundException("Course not found", $"No course with id {id}.");
        logger.LogInformation("Course {CourseId} deleted by {User}", id, currentUser.Username);
    }

    private static CourseResponse ToResponse(Course c) => new(c.Id, c.Code, c.Title, c.CreditHours, c.CreatedAtUtc);

    private static IQueryable<Course> Sort(IQueryable<Course> q, string field, bool desc) => field switch
    {
        "title" => desc ? q.OrderByDescending(c => c.Title).ThenBy(c => c.Id) : q.OrderBy(c => c.Title).ThenBy(c => c.Id),
        "credithours" => desc ? q.OrderByDescending(c => c.CreditHours).ThenBy(c => c.Id) : q.OrderBy(c => c.CreditHours).ThenBy(c => c.Id),
        "createdatutc" => desc ? q.OrderByDescending(c => c.CreatedAtUtc).ThenBy(c => c.Id) : q.OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id),
        "id" => desc ? q.OrderByDescending(c => c.Id) : q.OrderBy(c => c.Id),
        _ => desc ? q.OrderByDescending(c => c.Code).ThenBy(c => c.Id) : q.OrderBy(c => c.Code).ThenBy(c => c.Id)
    };
}
