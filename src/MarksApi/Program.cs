using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using MarksApi.Data;
using MarksApi.Endpoints;
using MarksApi.Infrastructure;
using MarksApi.Services;
using MarksApi.SmokeTests;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// The smoke test runs only on request: "--smoke-test" always, SMOKE_TEST=true everywhere except Production.
var smokeTest = args.Contains("--smoke-test")
    || (builder.Configuration.GetValue<bool>("SMOKE_TEST") && !builder.Environment.IsProduction());

if (smokeTest)
{
    // Private random port on the loopback interface, so it never clashes with a running copy of the API.
    builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
    builder.Logging.ClearProviders();
}

// ---------- Configuration ----------
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<GradingOptions>().Bind(builder.Configuration.GetSection(GradingOptions.Section)).ValidateDataAnnotations().ValidateOnStart();

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key is missing or shorter than 32 characters. Set it with user-secrets or the Jwt__Key environment variable.");

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Connection string 'Database' is missing. Set it with user-secrets or the ConnectionStrings__Database environment variable.");

// ---------- Database ----------
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

// ---------- Authentication and authorization ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "unique_name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.TeacherOnly, p => p.RequireRole(Roles.Teacher))
    .AddPolicy(Policies.AnyUser, p => p.RequireAuthenticatedUser());

// ---------- Errors, CORS, rate limiting, health, docs ----------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var loginPerMinute = builder.Configuration.GetValue("RateLimit:LoginPerMinute", 30);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.AddOpenApi();

// ---------- Application services (all scoped or stateless; no static mutable state) ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<DbSeeder>();

var app = builder.Build();

// ---------- Pipeline ----------
app.UseExceptionHandler();
app.UseStatusCodePages();           // 401, 403, 404, 429 ... also become problem details
if (!smokeTest) app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();        // interactive docs at /scalar

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
        });
    }
}).AllowAnonymous().WithTags("Health");

app.MapAuthEndpoints();
app.MapStudentEndpoints();
app.MapCourseEndpoints();
app.MapEnrollmentEndpoints();
app.MapReportEndpoints();

// ---------- Startup tasks ----------
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

if (app.Configuration.GetValue<bool>("Seed:Enabled"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(CancellationToken.None);
}

if (smokeTest)
{
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
    var exitCode = await SmokeTestRunner.RunAsync(address, app.Configuration);
    await app.StopAsync();
    return exitCode;
}

await app.RunAsync();
return 0;
