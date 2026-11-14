namespace MarksApi.Data;

public class Student
{
    public int Id { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<Enrollment> Enrollments { get; set; } = [];
}

public class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<Enrollment> Enrollments { get; set; } = [];
}

public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int? Mark { get; set; }
    public DateTime EnrolledAtUtc { get; set; }

    /// <summary>Mapped to PostgreSQL's xmin system column: optimistic concurrency without an extra column.</summary>
    public uint Version { get; set; }

    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public Student? Student { get; set; }
}
