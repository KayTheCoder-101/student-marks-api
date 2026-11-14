# Student Marks Management API

A production-style REST API for a university department: teachers create students and courses, enroll students, and record marks; a student can read only his or her own record and transcript.

Built with **ASP.NET Core (minimal APIs, .NET 10)**, **Entity Framework Core 10**, **PostgreSQL 16+**, **JWT** authentication, and tested with **Postman**, a built-in **smoke test**, and **xUnit**.

## Requirements

.NET 10 SDK, PostgreSQL 16 or later, Postman, Git. Docker Desktop is optional.

## Setup (step by step)

All commands run from the repository root unless stated.

### 1. Database and database user

```bash
psql -U postgres
CREATE USER marks_user WITH PASSWORD 'choose_a_password';
CREATE DATABASE marks_db OWNER marks_user;
\q
```

(Docker alternative: `docker compose up -d db` after creating `.env`, see "Docker" below.)

### 2. Secrets (never committed)

```bash
cd src/MarksApi
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=marks_db;Username=marks_user;Password=choose_a_password"
dotnet user-secrets set "Jwt:Key" "replace-with-a-long-random-text-of-32-or-more-characters"
cd ../..
```

`appsettings.json` holds only non-secret settings (JWT issuer/audience/lifetime, pass mark `Grading:PassMark` = 50, CORS origins, login rate limit). `appsettings.Example.json` lists every setting with fake values. On a server, supply secrets as environment variables, e.g. `ConnectionStrings__Database`, `Jwt__Key`.

### 3. Create the tables (migrations)

```bash
dotnet tool install --global dotnet-ef        # once
dotnet ef database update --project src/MarksApi
```

The `Migrations` folder is committed. After any entity change: `dotnet ef migrations add <Name> --project src/MarksApi`, then `database update`. The database is never edited by hand.

### 4. Run

```bash
dotnet run --project src/MarksApi
```

The console prints the address (e.g. `http://localhost:5123`; the port differs per computer, see `Properties/launchSettings.json`). In the Development environment the seed data is inserted automatically on the first start.

- Interactive API documentation (Scalar): `http://localhost:<port>/scalar`
- OpenAPI document: `http://localhost:<port>/openapi/v1.json`
- Health: `GET /health`

## Demo users (DEVELOPMENT ONLY)

Created by the seeder from `appsettings.Development.json`; stored hashed (ASP.NET Core `PasswordHasher`). Never use these on a real server.

| Username  | Password        | Role    | Linked student |
|-----------|-----------------|---------|----------------|
| teacher1  | `Teacher@12345` | Teacher | -              |
| student1  | `Student@12345` | Student | CS-2022-001 Ali Raza |
| student2  | `Student@12345` | Student | CS-2022-002 Sara Khan |

Seed data: 3 users, 2 students, 3 courses (CS401 3 cr, MA201 2 cr, PH101 4 cr) and 6 enrollments.

## Endpoints

| Method | Path | Who | Success |
|---|---|---|---|
| POST | /api/auth/login | anyone | 200 |
| GET | /api/students | Teacher | 200 |
| GET | /api/students/{id} | Teacher, or that student | 200 |
| POST | /api/students | Teacher | 201 |
| PUT | /api/students/{id} | Teacher | 200 |
| DELETE | /api/students/{id} | Teacher | 204 |
| GET | /api/courses | Teacher, Student | 200 |
| GET | /api/courses/{id} | Teacher, Student | 200 |
| POST | /api/courses | Teacher | 201 |
| PUT | /api/courses/{id} | Teacher | 200 |
| DELETE | /api/courses/{id} | Teacher | 204 |
| POST | /api/courses/{courseId}/enrollments | Teacher | 201 |
| GET | /api/courses/{courseId}/enrollments | Teacher | 200 |
| PUT | /api/enrollments/{id}/mark | Teacher | 200 |
| DELETE | /api/enrollments/{id} | Teacher | 204 |
| GET | /api/courses/{courseId}/stats | Teacher | 200 |
| GET | /api/students/{id}/transcript | Teacher, or that student | 200 |
| GET | /health | anyone | 200 / 503 |
| GET | /api/enrollments/{id} | Teacher | 200 (extra: target of the `Location` header) |

List endpoints accept `page`, `pageSize` (default 10, max 50, enforced by the server), `search` and `sort` (`field` or `-field`), and return `items, page, pageSize, totalCount`.

## Postman

1. Import `postman/MarksApi.postman_collection.json` and `postman/Local.postman_environment.json`.
2. Select the **Local** environment and set `baseUrl` to your address.
3. Run the API on a fresh database (Development seed enabled), then open **Collection Runner**, run the whole collection **in order**. Every request has a status test plus a body or header test; the login request stores the token by itself.

The collection creates its own uniquely named data (`PM-<runId>`) and deletes it again in the last folder, so it can be re-run.

## Smoke test

With the secrets from step 2 in place, add the smoke-test logins (the demo users above):

```bash
cd src/MarksApi
dotnet user-secrets set "SmokeTest:TeacherUsername" "teacher1"
dotnet user-secrets set "SmokeTest:TeacherPassword" "Teacher@12345"
dotnet user-secrets set "SmokeTest:StudentUsername" "student1"
dotnet user-secrets set "SmokeTest:StudentPassword" "Student@12345"
cd ../..
```

Run it (starts the API privately on a random local port, calls every endpoint with real HTTP and the real PostgreSQL database, prints PASS/FAIL per check, cleans up `SMOKE-` data, exits 0 only if all checks pass):

```bash
dotnet run --project src/MarksApi -- --smoke-test
```

A normal start never runs it. `SMOKE_TEST=true` also works but is ignored in the Production environment.

## Unit tests

```bash
dotnet test
```

xUnit tests cover the grading rules (weighted average, course statistics, pass rule), paging/sorting helpers and JWT creation. They use no in-memory or SQLite database.

## Docker (bonus)

```bash
cp .env.example .env        # then edit the values; .env is git-ignored
docker compose up --build
```

API on `http://localhost:8080` (docs at `/scalar`). The container applies migrations on start (`Database__MigrateOnStartup`) and seeds the demo data.

## Design decisions

- **Layers.** Endpoints (thin, `Endpoints/`) -> services behind interfaces (`Services/`) -> `AppDbContext` and entities (`Data/`); DTOs in `Dtos/`. Endpoints contain no queries or business rules. Pure grading maths lives in `GradeCalculator` so it can be unit tested.
- **409, not 400, for duplicates.** The request is well formed; it conflicts with the current state of the data.
- **Uniqueness is decided by the database.** Unique indexes on registration number, email, course code, username and (student, course). Services insert and translate the PostgreSQL unique violation (23505) into 409, instead of check-then-insert, so two simultaneous requests cannot both succeed. The same applies to deletes: `ON DELETE RESTRICT` (23001/23503) becomes 409.
- **Errors.** One format everywhere: problem details. A global exception handler maps domain exceptions to 400/403/404/409/401 and anything unexpected to a clean 500 without a stack trace; `UseStatusCodePages` covers framework-generated 401/403/404/429.
- **Validation** at the edge with data annotations through an endpoint filter (camelCase `errors` object), plus the same rules as database check constraints (credit hours 1-4, mark 0-100, lower-case email, role).
- **Auth.** Passwords hashed with `PasswordHasher`; JWT with expiry (claims: `sub`, `unique_name`, `role`, `student_id`). Role and "own data only" rules are enforced on the server (`TeacherOnly` policy plus `AccessRules.EnsureCanAccessStudent`). Login is rate limited per IP.
- **Reads** use `AsNoTracking` and `Select` projections into DTOs; every database call is async and receives the `CancellationToken`.
- **Time** is always UTC (`DateTime.UtcNow`, `timestamptz`).
- **Optimistic concurrency (bonus).** `enrollments` uses PostgreSQL `xmin` as a row version. `PUT /api/enrollments/{id}/mark` accepts an optional `version`; if the row changed meanwhile the API answers 409.
- **Extra endpoint.** `GET /api/enrollments/{id}` exists so the `Location` header of a new enrollment points to a real resource.

## Repository layout

```
src/MarksApi/      Program.cs, Data, Dtos, Endpoints, Infrastructure, Services, SmokeTests, Migrations
tests/MarksApi.Tests/
postman/           collection + environment
db/schema.sql      pg_dump --schema-only marks_db > db/schema.sql
docs/              erd.png, report.pdf, screenshots/
appsettings.Example.json, Dockerfile, docker-compose.yml, README.md, .gitignore
```
