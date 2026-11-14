using MarksApi.Infrastructure;
using MarksApi.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace MarksApi.Tests;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService() => new(Options.Create(new JwtOptions
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        Key = "unit-test-key-with-at-least-32-characters!!",
        ExpiryMinutes = 30
    }));

    [Fact]
    public void Create_PutsRoleUsernameAndStudentIdIntoTheToken()
    {
        var (token, _) = CreateService().Create(7, "student1", Roles.Student, studentId: 3);
        var jwt = new JsonWebToken(token);

        Assert.Equal("Student", jwt.GetClaim("role").Value);
        Assert.Equal("student1", jwt.GetClaim("unique_name").Value);
        Assert.Equal("3", jwt.GetClaim("student_id").Value);
        Assert.Equal("test-issuer", jwt.Issuer);
    }

    [Fact]
    public void Create_ExpiresInTheFuture_AndReportsTheSameExpiry()
    {
        var (token, expires) = CreateService().Create(1, "teacher1", Roles.Teacher, null);
        var jwt = new JsonWebToken(token);

        Assert.True(expires > DateTime.UtcNow);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
        Assert.True(Math.Abs((jwt.ValidTo - expires).TotalSeconds) < 2);
    }

    [Fact]
    public void Create_OmitsStudentId_ForTeachers()
    {
        var (token, _) = CreateService().Create(1, "teacher1", Roles.Teacher, null);
        Assert.False(new JsonWebToken(token).TryGetClaim("student_id", out _));
    }
}
