using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public interface IStudentService
{
    Task<PagedResult<StudentResponse>> ListAsync(PageQuery query, CancellationToken ct);
    Task<StudentResponse> GetAsync(int id, CancellationToken ct);
    Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken ct);
    Task<StudentResponse> UpdateAsync(int id, UpdateStudentRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class StudentService(AppDbContext db, ICurrentUser currentUser, ILogger<StudentService> logger) : IStudentService
{
    private static readonly string[] SortFields = ["id", "registrationno", "fullname", "email", "createdatutc"];

    private const string DuplicateTitle = "Duplicate student";
    private const string DuplicateDetail = "A student with this registration number or email already exists.";

    public async Task<PagedResult<StudentResponse>> ListAsync(PageQuery q, CancellationToken ct)
    {
        var (page, pageSize) = Paging.Normalize(q.Page, q.PageSize);
        var (field, desc) = Paging.ParseSort(q.Sort, "fullname", SortFields);

        IQueryable<Student> query = db.Students.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var pattern = $"%{Paging.EscapeLike(q.Search.Trim())}%";
            query = query.Where(s => EF.Functions.ILike(s.FullName, pattern)
                                  || EF.Functions.ILike(s.RegistrationNo, pattern)
                                  || EF.Functions.ILike(s.Email, pattern));
        }

        query = Sort(query, field, desc);

        return await query
            .Select(s => new StudentResponse(s.Id, s.RegistrationNo, s.FullName, s.Email))
            .ToPagedAsync(page, pageSize, ct);
    }

    public async Task<StudentResponse> GetAsync(int id, CancellationToken ct)
    {
        currentUser.EnsureCanAccessStudent(id);

        return await db.Students.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StudentResponse(s.Id, s.RegistrationNo, s.FullName, s.Email))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Student not found", $"No student with id {id}.");
    }

    public async Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken ct)
    {
        var student = new Student
        {
            RegistrationNo = request.RegistrationNo.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Students.Add(student);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            logger.LogWarning("Create student rejected: duplicate registration number {RegistrationNo} or email", student.RegistrationNo);
            throw new ConflictException(DuplicateTitle, DuplicateDetail);
        }

        logger.LogInformation("Student {StudentId} created ({RegistrationNo}) by {User}", student.Id, student.RegistrationNo, currentUser.Username);
        return ToResponse(student);
    }

    public async Task<StudentResponse> UpdateAsync(int id, UpdateStudentRequest request, CancellationToken ct)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Student not found", $"No student with id {id}.");

        student.RegistrationNo = request.RegistrationNo.Trim();
        student.FullName = request.FullName.Trim();
        student.Email = request.Email.Trim().ToLowerInvariant();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            logger.LogWarning("Update of student {StudentId} rejected: duplicate registration number or email", id);
            throw new ConflictException(DuplicateTitle, DuplicateDetail);
        }

        logger.LogInformation("Student {StudentId} updated by {User}", id, currentUser.Username);
        return ToResponse(student);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        int deleted;
        try
        {
            // No check-then-delete: let the database enforce ON DELETE RESTRICT.
            deleted = await db.Students.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        }
        catch (Exception ex) when (DbErrors.IsStillReferenced(ex))
        {
            logger.LogWarning("Delete of student {StudentId} rejected: still referenced", id);
            throw new ConflictException("Student cannot be deleted", "The student still has enrollments (or a user account).");
        }

        if (deleted == 0) throw new NotFoundException("Student not found", $"No student with id {id}.");
        logger.LogInformation("Student {StudentId} deleted by {User}", id, currentUser.Username);
    }

    private static StudentResponse ToResponse(Student s) => new(s.Id, s.RegistrationNo, s.FullName, s.Email);

    private static IQueryable<Student> Sort(IQueryable<Student> q, string field, bool desc) => field switch
    {
        "registrationno" => desc ? q.OrderByDescending(s => s.RegistrationNo).ThenBy(s => s.Id) : q.OrderBy(s => s.RegistrationNo).ThenBy(s => s.Id),
        "email" => desc ? q.OrderByDescending(s => s.Email).ThenBy(s => s.Id) : q.OrderBy(s => s.Email).ThenBy(s => s.Id),
        "createdatutc" => desc ? q.OrderByDescending(s => s.CreatedAtUtc).ThenBy(s => s.Id) : q.OrderBy(s => s.CreatedAtUtc).ThenBy(s => s.Id),
        "id" => desc ? q.OrderByDescending(s => s.Id) : q.OrderBy(s => s.Id),
        _ => desc ? q.OrderByDescending(s => s.FullName).ThenBy(s => s.Id) : q.OrderBy(s => s.FullName).ThenBy(s => s.Id)
    };
}
