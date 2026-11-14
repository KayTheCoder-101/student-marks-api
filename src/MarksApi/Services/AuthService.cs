using MarksApi.Data;
using MarksApi.Dtos;
using MarksApi.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
}

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    IJwtTokenService jwt,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);

        // Same answer for "unknown user" and "wrong password" so usernames cannot be probed.
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Login failed for username {Username}", username);
            throw new UnauthorizedAppException("Invalid credentials", "The username or password is incorrect.");
        }

        var (token, expires) = jwt.Create(user.Id, user.Username, user.Role, user.StudentId);
        logger.LogInformation("User {Username} ({Role}) logged in", user.Username, user.Role);
        return new LoginResponse(token, expires, user.Role);
    }
}
