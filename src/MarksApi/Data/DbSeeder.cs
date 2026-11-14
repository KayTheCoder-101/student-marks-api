using MarksApi.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

/// <summary>Development seed data. Passwords come from configuration (Seed:*) and are stored hashed.</summary>
public sealed class DbSeeder(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration config, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var teacherPassword = config["Seed:TeacherPassword"];
        var studentPassword = config["Seed:StudentPassword"];
        if (string.IsNullOrWhiteSpace(teacherPassword) || string.IsNullOrWhiteSpace(studentPassword))
            throw new InvalidOperationException("Seed is enabled but Seed:TeacherPassword / Seed:StudentPassword are not set.");

        var now = DateTime.UtcNow;

        var s1 = new Student { RegistrationNo = "CS-2022-001", FullName = "Ali Raza", Email = "ali.raza@example.com", CreatedAtUtc = now };
        var s2 = new Student { RegistrationNo = "CS-2022-002", FullName = "Sara Khan", Email = "sara.khan@example.com", CreatedAtUtc = now };

        var c1 = new Course { Code = "CS401", Title = "Advanced Programming", CreditHours = 3, CreatedAtUtc = now };
        var c2 = new Course { Code = "MA201", Title = "Linear Algebra", CreditHours = 2, CreatedAtUtc = now };
        var c3 = new Course { Code = "PH101", Title = "Physics", CreditHours = 4, CreatedAtUtc = now };

        db.Enrollments.AddRange(
            new Enrollment { Student = s1, Course = c1, Mark = 80, EnrolledAtUtc = now },
            new Enrollment { Student = s1, Course = c2, Mark = 60, EnrolledAtUtc = now },
            new Enrollment { Student = s1, Course = c3, Mark = null, EnrolledAtUtc = now },
            new Enrollment { Student = s2, Course = c1, Mark = 70, EnrolledAtUtc = now },
            new Enrollment { Student = s2, Course = c2, Mark = 90, EnrolledAtUtc = now },
            new Enrollment { Student = s2, Course = c3, Mark = 55, EnrolledAtUtc = now });

        db.Users.AddRange(
            NewUser("teacher1", teacherPassword, Roles.Teacher, null),
            NewUser("student1", studentPassword, Roles.Student, s1),
            NewUser("student2", studentPassword, Roles.Student, s2));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Development seed data inserted (users, students, courses, enrollments).");
    }

    private User NewUser(string username, string password, string role, Student? student)
    {
        var user = new User { Username = username, Role = role, Student = student };
        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }
}
