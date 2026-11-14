using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(e =>
        {
            e.ToTable("students", t => t.HasCheckConstraint("ck_students_email_lowercase", "email = lower(email)"));
            e.HasKey(x => x.Id);
            e.Property(x => x.RegistrationNo).HasMaxLength(20).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
            e.HasIndex(x => x.RegistrationNo).IsUnique().HasDatabaseName("ux_students_registration_no");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_students_email");
        });

        modelBuilder.Entity<Course>(e =>
        {
            e.ToTable("courses", t => t.HasCheckConstraint("ck_courses_credit_hours", "credit_hours BETWEEN 1 AND 4"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(10).IsRequired();
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.Property(x => x.CreditHours).IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
            e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_courses_code");
        });

        modelBuilder.Entity<Enrollment>(e =>
        {
            e.ToTable("enrollments", t => t.HasCheckConstraint("ck_enrollments_mark", "mark IS NULL OR mark BETWEEN 0 AND 100"));
            e.HasKey(x => x.Id);
            e.Property(x => x.EnrolledAtUtc).HasColumnType("timestamp with time zone");
            e.Property(x => x.Version).IsRowVersion();

            e.HasOne(x => x.Student).WithMany(s => s.Enrollments)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Course).WithMany(c => c.Enrollments)
                .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);

            // The database guarantees "enroll once", even when two requests arrive together.
            e.HasIndex(x => new { x.StudentId, x.CourseId }).IsUnique().HasDatabaseName("ux_enrollments_student_course");
            e.HasIndex(x => x.CourseId).HasDatabaseName("ix_enrollments_course_id");
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users", t => t.HasCheckConstraint("ck_users_role", "role IN ('Teacher', 'Student')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(50).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnType("text").IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Student).WithMany()
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Username).IsUnique().HasDatabaseName("ux_users_username");
        });
    }
}
