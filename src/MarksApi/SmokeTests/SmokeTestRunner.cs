using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MarksApi.SmokeTests;

/// <summary>
/// Startup smoke test. Runs only with "--smoke-test" (or SMOKE_TEST=true outside Production).
/// Sends real HTTP requests to the running API (real PostgreSQL), prints PASS/FAIL per check,
/// deletes everything it created (prefix SMOKE-) and returns the process exit code.
/// </summary>
public sealed class SmokeTestRunner
{
    private const string Prefix = "SMOKE-";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private int _passed;
    private int _failed;
    private string _teacherUser = string.Empty;

    private SmokeTestRunner(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public static async Task<int> RunAsync(string baseUrl, IConfiguration config)
    {
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
        return await new SmokeTestRunner(http, config).ExecuteAsync();
    }

    private async Task<int> ExecuteAsync()
    {
        Console.WriteLine($"Smoke test against {_http.BaseAddress}");
        Console.WriteLine(new string('-', 100));

        string? teacherToken = null;
        try
        {
            _teacherUser = Require("SmokeTest:TeacherUsername");
            var teacherPassword = Require("SmokeTest:TeacherPassword");
            var studentUser = Require("SmokeTest:StudentUsername");
            var studentPassword = Require("SmokeTest:StudentPassword");

            var (_, tBody) = await Call("teacher login", HttpMethod.Post, "/api/auth/login", 200,
                new { username = _teacherUser, password = teacherPassword }, null, b => b.TryGetProperty("token", out _));
            teacherToken = tBody.ValueKind == JsonValueKind.Object ? tBody.GetProperty("token").GetString() : null;

            var (_, sBody) = await Call("student login", HttpMethod.Post, "/api/auth/login", 200,
                new { username = studentUser, password = studentPassword }, null, b => b.TryGetProperty("token", out _));
            var studentToken = sBody.ValueKind == JsonValueKind.Object ? sBody.GetProperty("token").GetString() : null;

            if (teacherToken is null || studentToken is null)
                throw new InvalidOperationException("Cannot continue without a Teacher and a Student token.");

            await CleanupAsync(teacherToken);          // leftovers from an earlier failed run
            await RunChecksAsync(teacherToken, studentToken);
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"FAIL  unexpected problem: {ex.Message}");
        }
        finally
        {
            if (teacherToken is not null)
            {
                try { await CleanupAsync(teacherToken); }
                catch (Exception ex) { Console.WriteLine($"WARN  cleanup problem: {ex.Message}"); }
            }
        }

        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"Smoke test finished: {_passed} passed, {_failed} failed, {_passed + _failed} checks.");
        return _failed == 0 ? 0 : 1;
    }

    private async Task RunChecksAsync(string t, string s)
    {
        var ownStudentId = StudentIdFromToken(s);

        // ---- Health and login failures
        await Call("health", HttpMethod.Get, "/health", 200, null, null, b => b.GetProperty("status").GetString() == "Healthy");
        await Call("login wrong password", HttpMethod.Post, "/api/auth/login", 401, new { username = _teacherUser, password = "definitely-wrong" });
        await Call("login missing fields", HttpMethod.Post, "/api/auth/login", 400, new { });

        // ---- Students
        await Call("list students", HttpMethod.Get, "/api/students?pageSize=5", 200, null, t, b => b.GetProperty("pageSize").GetInt32() == 5);
        await Call("list students, no token", HttpMethod.Get, "/api/students", 401);
        await Call("list students as Student", HttpMethod.Get, "/api/students", 403, null, s);

        var (_, a) = await Call("create student A", HttpMethod.Post, "/api/students", 201,
            new { registrationNo = $"{Prefix}S001", fullName = "SMOKE Alice", email = "smoke-alice@example.com" }, t);
        var studentA = IdOf(a);
        await Call("create student, duplicate", HttpMethod.Post, "/api/students", 409,
            new { registrationNo = $"{Prefix}S001", fullName = "SMOKE Alice", email = "smoke-other@example.com" }, t);
        await Call("create student, empty name", HttpMethod.Post, "/api/students", 400,
            new { registrationNo = $"{Prefix}S099", fullName = "", email = "smoke-x@example.com" }, t);
        await Call("create student, no token", HttpMethod.Post, "/api/students", 401,
            new { registrationNo = $"{Prefix}S098", fullName = "x", email = "smoke-y@example.com" });
        await Call("create student as Student", HttpMethod.Post, "/api/students", 403,
            new { registrationNo = $"{Prefix}S097", fullName = "x", email = "smoke-z@example.com" }, s);

        var (_, b) = await Call("create student B", HttpMethod.Post, "/api/students", 201,
            new { registrationNo = $"{Prefix}S002", fullName = "SMOKE Bob", email = "smoke-bob@example.com" }, t);
        var studentB = IdOf(b);

        await Call("get student", HttpMethod.Get, $"/api/students/{studentA}", 200, null, t, x => x.GetProperty("id").GetInt32() == studentA);
        await Call("get student, unknown id", HttpMethod.Get, "/api/students/999999999", 404, null, t);
        await Call("get student, no token", HttpMethod.Get, $"/api/students/{studentA}", 401);
        await Call("get student, Student reads own record", HttpMethod.Get, $"/api/students/{ownStudentId}", 200, null, s);
        await Call("get student, Student reads another", HttpMethod.Get, $"/api/students/{studentA}", 403, null, s);

        await Call("update student A", HttpMethod.Put, $"/api/students/{studentA}", 200,
            new { registrationNo = $"{Prefix}S001", fullName = "SMOKE Alice Updated", email = "smoke-alice@example.com" }, t,
            x => x.GetProperty("fullName").GetString() == "SMOKE Alice Updated");
        await Call("update student, unknown id", HttpMethod.Put, "/api/students/999999999", 404,
            new { registrationNo = $"{Prefix}S050", fullName = "x", email = "smoke-n@example.com" }, t);
        await Call("update student, bad body", HttpMethod.Put, $"/api/students/{studentA}", 400,
            new { registrationNo = "", fullName = "x", email = "not-an-email" }, t);
        await Call("update student B to A's registration number", HttpMethod.Put, $"/api/students/{studentB}", 409,
            new { registrationNo = $"{Prefix}S001", fullName = "SMOKE Bob", email = "smoke-bob@example.com" }, t);
        await Call("update student as Student", HttpMethod.Put, $"/api/students/{studentA}", 403,
            new { registrationNo = $"{Prefix}S001", fullName = "x", email = "smoke-alice@example.com" }, s);

        // ---- Courses
        await Call("list courses as Teacher", HttpMethod.Get, "/api/courses", 200, null, t);
        await Call("list courses as Student", HttpMethod.Get, "/api/courses", 200, null, s);
        await Call("list courses, no token", HttpMethod.Get, "/api/courses", 401);

        var (_, c) = await Call("create course C", HttpMethod.Post, "/api/courses", 201,
            new { code = $"{Prefix}C01", title = "SMOKE Course One", creditHours = 3 }, t);
        var courseC = IdOf(c);
        var (_, d) = await Call("create course D", HttpMethod.Post, "/api/courses", 201,
            new { code = $"{Prefix}C02", title = "SMOKE Course Two", creditHours = 2 }, t);
        var courseD = IdOf(d);
        await Call("create course, duplicate code", HttpMethod.Post, "/api/courses", 409,
            new { code = $"{Prefix}C01", title = "again", creditHours = 1 }, t);
        await Call("create course, credit hours 5", HttpMethod.Post, "/api/courses", 400,
            new { code = $"{Prefix}C03", title = "bad", creditHours = 5 }, t);
        await Call("create course as Student", HttpMethod.Post, "/api/courses", 403,
            new { code = $"{Prefix}C04", title = "no", creditHours = 1 }, s);

        await Call("get course", HttpMethod.Get, $"/api/courses/{courseC}", 200, null, s, x => x.GetProperty("creditHours").GetInt32() == 3);
        await Call("get course, unknown id", HttpMethod.Get, "/api/courses/999999999", 404, null, t);
        await Call("get course, no token", HttpMethod.Get, $"/api/courses/{courseC}", 401);

        await Call("update course C", HttpMethod.Put, $"/api/courses/{courseC}", 200,
            new { code = $"{Prefix}C01", title = "SMOKE Course One Updated", creditHours = 3 }, t,
            x => x.GetProperty("title").GetString() == "SMOKE Course One Updated");
        await Call("update course, unknown id", HttpMethod.Put, "/api/courses/999999999", 404,
            new { code = $"{Prefix}C09", title = "x", creditHours = 1 }, t);
        await Call("update course, bad body", HttpMethod.Put, $"/api/courses/{courseC}", 400,
            new { code = "", title = "", creditHours = 9 }, t);
        await Call("update course D to C's code", HttpMethod.Put, $"/api/courses/{courseD}", 409,
            new { code = $"{Prefix}C01", title = "SMOKE Course Two", creditHours = 2 }, t);
        await Call("update course as Student", HttpMethod.Put, $"/api/courses/{courseC}", 403,
            new { code = $"{Prefix}C01", title = "x", creditHours = 3 }, s);

        // ---- Enrollments and marks
        var (_, e1) = await Call("enroll A in C", HttpMethod.Post, $"/api/courses/{courseC}/enrollments", 201, new { studentId = studentA }, t);
        var enrollA = IdOf(e1);
        var (_, e2) = await Call("enroll B in C", HttpMethod.Post, $"/api/courses/{courseC}/enrollments", 201, new { studentId = studentB }, t);
        var enrollB = IdOf(e2);
        await Call("enroll A in C again", HttpMethod.Post, $"/api/courses/{courseC}/enrollments", 409, new { studentId = studentA }, t);
        await Call("enroll in unknown course", HttpMethod.Post, "/api/courses/999999999/enrollments", 404, new { studentId = studentA }, t);
        await Call("enroll unknown student", HttpMethod.Post, $"/api/courses/{courseC}/enrollments", 404, new { studentId = 999999999 }, t);
        await Call("enroll, bad body", HttpMethod.Post, $"/api/courses/{courseC}/enrollments", 400, new { studentId = 0 }, t);
        await Call("enroll, no token", HttpMethod.Post, $"/api/courses/{courseC}/enrollments", 401, new { studentId = studentA });
        await Call("enroll as Student", HttpMethod.Post, $"/api/courses/{courseC}/enrollments", 403, new { studentId = studentA }, s);

        await Call("list enrollments of C", HttpMethod.Get, $"/api/courses/{courseC}/enrollments", 200, null, t, x => x.GetProperty("totalCount").GetInt32() == 2);
        await Call("list enrollments, unknown course", HttpMethod.Get, "/api/courses/999999999/enrollments", 404, null, t);
        await Call("list enrollments, as Student", HttpMethod.Get, $"/api/courses/{courseC}/enrollments", 403, null, s);

        await Call("get enrollment", HttpMethod.Get, $"/api/enrollments/{enrollA}", 200, null, t, x => x.GetProperty("courseId").GetInt32() == courseC);
        await Call("get enrollment, unknown id", HttpMethod.Get, "/api/enrollments/999999999", 404, null, t);

        await Call("set mark 80 for A", HttpMethod.Put, $"/api/enrollments/{enrollA}/mark", 200, new { mark = 80 }, t, x => x.GetProperty("mark").GetInt32() == 80);
        await Call("set mark 70 for B", HttpMethod.Put, $"/api/enrollments/{enrollB}/mark", 200, new { mark = 70 }, t, x => x.GetProperty("mark").GetInt32() == 70);
        await Call("set mark 101", HttpMethod.Put, $"/api/enrollments/{enrollA}/mark", 400, new { mark = 101 }, t);
        await Call("set mark -1", HttpMethod.Put, $"/api/enrollments/{enrollA}/mark", 400, new { mark = -1 }, t);
        await Call("set mark, unknown enrollment", HttpMethod.Put, "/api/enrollments/999999999/mark", 404, new { mark = 50 }, t);
        await Call("set mark, no token", HttpMethod.Put, $"/api/enrollments/{enrollA}/mark", 401, new { mark = 50 });
        await Call("set mark as Student", HttpMethod.Put, $"/api/enrollments/{enrollA}/mark", 403, new { mark = 50 }, s);

        // ---- Reports
        await Call("course stats", HttpMethod.Get, $"/api/courses/{courseC}/stats", 200, null, t, x =>
            x.GetProperty("enrolled").GetInt32() == 2 && x.GetProperty("graded").GetInt32() == 2
            && Math.Abs(x.GetProperty("average").GetDouble() - 75.0) < 0.01
            && x.GetProperty("highest").GetInt32() == 80 && x.GetProperty("lowest").GetInt32() == 70);
        await Call("course stats, unknown course", HttpMethod.Get, "/api/courses/999999999/stats", 404, null, t);
        await Call("course stats as Student", HttpMethod.Get, $"/api/courses/{courseC}/stats", 403, null, s);

        await Call("transcript of A", HttpMethod.Get, $"/api/students/{studentA}/transcript", 200, null, t,
            x => Math.Abs(x.GetProperty("weightedAverage").GetDouble() - 80.0) < 0.01);
        await Call("transcript, unknown student", HttpMethod.Get, "/api/students/999999999/transcript", 404, null, t);
        await Call("transcript, no token", HttpMethod.Get, $"/api/students/{studentA}/transcript", 401);
        await Call("transcript, Student reads own", HttpMethod.Get, $"/api/students/{ownStudentId}/transcript", 200, null, s);
        await Call("transcript, Student reads another", HttpMethod.Get, $"/api/students/{studentA}/transcript", 403, null, s);

        // ---- Deletes (409 first, then the valid deletes in a safe order)
        await Call("delete student with enrollments", HttpMethod.Delete, $"/api/students/{studentA}", 409, null, t);
        await Call("delete course with enrollments", HttpMethod.Delete, $"/api/courses/{courseC}", 409, null, t);
        await Call("delete student, no token", HttpMethod.Delete, $"/api/students/{studentA}", 401);
        await Call("delete student as Student", HttpMethod.Delete, $"/api/students/{studentA}", 403, null, s);
        await Call("delete course as Student", HttpMethod.Delete, $"/api/courses/{courseC}", 403, null, s);
        await Call("delete enrollment as Student", HttpMethod.Delete, $"/api/enrollments/{enrollA}", 403, null, s);
        await Call("delete enrollment, no token", HttpMethod.Delete, $"/api/enrollments/{enrollA}", 401);

        await Call("delete enrollment A", HttpMethod.Delete, $"/api/enrollments/{enrollA}", 204, null, t);
        await Call("delete enrollment A again", HttpMethod.Delete, $"/api/enrollments/{enrollA}", 404, null, t);
        await Call("delete enrollment B", HttpMethod.Delete, $"/api/enrollments/{enrollB}", 204, null, t);
        await Call("delete student A", HttpMethod.Delete, $"/api/students/{studentA}", 204, null, t);
        await Call("delete student A again", HttpMethod.Delete, $"/api/students/{studentA}", 404, null, t);
        await Call("delete course C", HttpMethod.Delete, $"/api/courses/{courseC}", 204, null, t);
        await Call("delete course C again", HttpMethod.Delete, $"/api/courses/{courseC}", 404, null, t);
        // Student B and course D are removed by the final cleanup.
    }

    /// <summary>Removes every record whose name starts with SMOKE- (through the API itself).</summary>
    private async Task CleanupAsync(string token)
    {
        var courses = await Quiet(HttpMethod.Get, $"/api/courses?search={Prefix}&pageSize=50", token);
        foreach (var course in Items(courses))
        {
            var courseId = course.GetProperty("id").GetInt32();
            var enrollments = await Quiet(HttpMethod.Get, $"/api/courses/{courseId}/enrollments?pageSize=50", token);
            foreach (var enrollment in Items(enrollments))
                await Quiet(HttpMethod.Delete, $"/api/enrollments/{enrollment.GetProperty("id").GetInt32()}", token);
            await Quiet(HttpMethod.Delete, $"/api/courses/{courseId}", token);
        }

        var students = await Quiet(HttpMethod.Get, $"/api/students?search={Prefix}&pageSize=50", token);
        foreach (var student in Items(students))
            await Quiet(HttpMethod.Delete, $"/api/students/{student.GetProperty("id").GetInt32()}", token);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<(int Status, JsonElement Body)> Call(
        string label, HttpMethod method, string path, int expected,
        object? body = null, string? token = null, Func<JsonElement, bool>? verify = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);

        using var response = await _http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var actual = (int)response.StatusCode;
        var json = Parse(text);

        var ok = actual == expected;
        var note = string.Empty;
        if (ok && expected == 201 && response.Headers.Location is null) { ok = false; note = " (Location header missing)"; }
        if (ok && verify is not null && !SafeVerify(verify, json)) { ok = false; note = " (response body check failed)"; }

        if (ok) _passed++; else _failed++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {method.Method,-6} {path,-52} expected {expected}  actual {actual}  - {label}{note}");
        return (actual, json);
    }

    private async Task<JsonElement> Quiet(HttpMethod method, string path, string token)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request);
        return Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return default;
        try { return JsonDocument.Parse(text).RootElement.Clone(); }
        catch (JsonException) { return default; }
    }

    private static bool SafeVerify(Func<JsonElement, bool> verify, JsonElement json)
    {
        try { return verify(json); }
        catch (Exception) { return false; }
    }

    private static IEnumerable<JsonElement> Items(JsonElement page) =>
        page.ValueKind == JsonValueKind.Object && page.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().ToList()
            : [];

    private static int IdOf(JsonElement body) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty("id", out var id) ? id.GetInt32() : -1;

    private static int StudentIdFromToken(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
        return int.Parse(doc.RootElement.GetProperty("student_id").GetString()!);
    }

    private string Require(string key) =>
        _config[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Configuration value '{key}' is missing. Set it with user-secrets or an environment variable (see README).");
}
