using System.Security.Claims;

namespace MarksApi.Infrastructure;

public static class Roles
{
    public const string Teacher = "Teacher";
    public const string Student = "Student";
}

public static class Policies
{
    public const string TeacherOnly = "TeacherOnly";
    public const string AnyUser = "AnyUser";
}

public interface ICurrentUser
{
    bool IsTeacher { get; }
    int? StudentId { get; }
    string? Username { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsTeacher => Principal?.IsInRole(Roles.Teacher) ?? false;
    public string? Username => Principal?.FindFirst("unique_name")?.Value;
    public int? StudentId => int.TryParse(Principal?.FindFirst("student_id")?.Value, out var id) ? id : null;
}

public static class AccessRules
{
    /// <summary>A Teacher may read anyone. A Student may read only the student record linked to the account.</summary>
    public static void EnsureCanAccessStudent(this ICurrentUser user, int studentId)
    {
        if (user.IsTeacher) return;
        if (user.StudentId == studentId) return;
        throw new ForbiddenException("Forbidden", "You may only read your own student data.");
    }
}
