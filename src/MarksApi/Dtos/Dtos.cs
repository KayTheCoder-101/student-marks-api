using System.ComponentModel.DataAnnotations;

namespace MarksApi.Dtos;

// ---------- Common ----------
public sealed record PageQuery(int? Page, int? PageSize, string? Search, string? Sort);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

// ---------- Auth ----------
public sealed class LoginRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, ErrorMessage = "Username must be at most 50 characters.")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(200, ErrorMessage = "Password must be at most 200 characters.")]
    public string Password { get; init; } = string.Empty;
}

public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, string Role);

// ---------- Students ----------
public class StudentRequest
{
    [Required(ErrorMessage = "Registration number is required.")]
    [StringLength(20, ErrorMessage = "Registration number must be at most 20 characters.")]
    public string RegistrationNo { get; init; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, ErrorMessage = "Full name must be at most 100 characters.")]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(150, ErrorMessage = "Email must be at most 150 characters.")]
    [EmailAddress(ErrorMessage = "Email is not a valid address.")]
    public string Email { get; init; } = string.Empty;
}

public sealed class CreateStudentRequest : StudentRequest;
public sealed class UpdateStudentRequest : StudentRequest;

public sealed record StudentResponse(int Id, string RegistrationNo, string FullName, string Email);

// ---------- Courses ----------
public class CourseRequest
{
    [Required(ErrorMessage = "Course code is required.")]
    [StringLength(10, ErrorMessage = "Course code must be at most 10 characters.")]
    public string Code { get; init; } = string.Empty;

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(150, ErrorMessage = "Title must be at most 150 characters.")]
    public string Title { get; init; } = string.Empty;

    [Range(1, 4, ErrorMessage = "Credit hours must be between 1 and 4.")]
    public int CreditHours { get; init; }
}

public sealed class CreateCourseRequest : CourseRequest;
public sealed class UpdateCourseRequest : CourseRequest;

public sealed record CourseResponse(int Id, string Code, string Title, int CreditHours, DateTime CreatedAtUtc);

// ---------- Enrollments and marks ----------
public sealed class EnrollStudentRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid studentId is required.")]
    public int StudentId { get; init; }
}

public sealed class UpdateMarkRequest
{
    [Required(ErrorMessage = "Mark is required.")]
    [Range(0, 100, ErrorMessage = "Mark must be a whole number from 0 to 100.")]
    public int? Mark { get; init; }

    /// <summary>Optional. If sent, the update is rejected with 409 when someone else changed the enrollment meanwhile.</summary>
    public uint? Version { get; init; }
}

public sealed record EnrollmentResponse(int Id, int StudentId, int CourseId, int? Mark, DateTime EnrolledAtUtc, uint Version);

public sealed record CourseEnrollmentItem(int Id, int StudentId, string RegistrationNo, string FullName, int? Mark, DateTime EnrolledAtUtc);

// ---------- Reports ----------
public sealed record CourseStatsResponse(int CourseId, string CourseCode, int Enrolled, int Graded, double? Average, int? Highest, int? Lowest, int Passed);

public sealed record TranscriptCourse(string Code, int CreditHours, int? Mark, bool Passed);

public sealed record TranscriptResponse(int StudentId, string RegistrationNo, string FullName, IReadOnlyList<TranscriptCourse> Courses, double? WeightedAverage);
