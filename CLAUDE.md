# CLAUDE.md
 
This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.
 
## Build & Run
 
```bash
# Build entire solution
dotnet build Pdmt.slnx
 
# Run API (development)
dotnet run --project Pdmt.Api
 
# Run Blazor WASM client
dotnet run --project Pdmt.Client
 
# Run all tests
dotnet test Pdmt.Api.Tests/Pdmt.Api.Tests.csproj
 
# Run a single test by name
dotnet test Pdmt.Api.Tests/Pdmt.Api.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
 
# Start dev infrastructure (PostgreSQL + Redis)
docker compose up -d

# Add a new EF migration
dotnet ef migrations add <MigrationName> --project Pdmt.Api

# Apply EF migrations
dotnet ef database update --project Pdmt.Api

# Build MAUI project (Pdmt.slnx is not supported by msbuild directly)
dotnet build Pdmt.Maui/Pdmt.Maui.csproj
```
 
## Code Style (.NET 8 / C# 12)
 
- Use **primary constructors** for services and classes where applicable
- Use **collection expressions** (`[1, 2, 3]`, `[..a, ..b]`) instead of `new List<T> {}` or `Array.Empty<T>()`
- Use **`is null` / `is not null`** instead of `== null` / `!= null`
- Prefer **pattern matching** (`switch` expressions, `is` patterns) over chains of `if`/`else`
- Use **`required` properties** on DTOs/models instead of constructor enforcement where it reads more clearly
- Enable and respect **nullable reference types** (`<Nullable>enable</Nullable>` is on); annotate accordingly
- Use `async`/`await` throughout — never `.Result` or `.Wait()`
- Prefer `IReadOnlyList<T>` / `IReadOnlyCollection<T>` for return types that callers should not mutate
- Target-typed `new()` where the type is obvious from context
 
## Architecture
 
**Pdmt** is a personal event tracking app. Users log events (positive/negative experiences) with metadata: type, category, intensity, context, relationship flag, and influenceability.
 
### Projects
 
- **Pdmt.Api** — ASP.NET Core 8 REST API (backend)
- **Pdmt.Api.Tests** — xUnit tests using `Microsoft.AspNetCore.Mvc.Testing` with an in-memory EF database
- **Pdmt.Client** — Blazor WebAssembly frontend with MudBlazor UI
- **Pdmt.Maui** — .NET MAUI Android client
 
### API Layers
 
- **Controllers** → **Services** → **AppDbContext** (EF Core)
- Services (`AuthService`, `EventService`, `TagService`, `AnalyticsService`, `UserService`) contain all business logic; controllers are thin
- Controllers: `AuthController`, `EventsController`, `TagsController`, `AnalyticsController`
- `TokenCleanupBgService` — background service that purges expired refresh tokens
- Global exception handling via `ExceptionHandlingMiddleware` — do not add try/catch in controllers

### Exception → HTTP Status

`ExceptionHandlingMiddleware` maps exceptions to HTTP codes — throw from services, never catch in controllers:
- `UnauthorizedAccessException` → 401
- `NotFoundException` (`Infrastructure/Exceptions/`) → 404
- `InvalidOperationException` → 400
- `RateLimitExceededException` → 429
 
### Authentication
 
- JWT Bearer tokens (60 min access token, 1 day refresh token)
- Refresh tokens are SHA256-hashed before storage; never stored in plaintext
- Token rotation: old refresh tokens are invalidated on login/refresh
- `POST /api/auth/login` → `POST /api/auth/refresh` → `POST /api/auth/logout`
 
### Tags

- Tags are per-user; `Tag` has a `UserId` FK — never shared across users
- `EventTag` is a join entity for the Event↔Tag many-to-many (no direct EF `HasMany(...).WithMany`)
- `TagService.UpsertTagAsync` is idempotent — returns existing tag if name matches (case-sensitive, trimmed)

### Rate Limiting
 
Composite pattern: tries **Redis** first, falls back to **in-memory** if Redis is unavailable.
- `CompositeRateLimitService` wraps `RedisRateLimitService` + `InMemoryRateLimitService`
- Rules are configured in `appsettings.json` under `RateLimiting.Rules` (keyed by action name like `"Auth.Login"`)
 
### Database
 
- **Development:** PostgreSQL via Docker Compose (`appsettings.Development.json`)
- **Production:** PostgreSQL — required env vars listed in `.env.example`
- `AppDbContext` has composite indexes on `Events(UserId, Timestamp)` and `Events(UserId, Type)`
- User data is isolated — all queries filter by `UserId` extracted from JWT claims
 
### Frontend Client (Pdmt.Client)
 
- `AuthHeaderHandler` (delegating handler) injects `Authorization: Bearer` on API calls
- `TokenService` reads/writes tokens from storage
- `AuthService` handles login/logout/refresh flows
- API base URL configured via `appsettings.json` → `PdmtApi.BaseUrl`
 
### Testing
 
`CustomWebAppFactory` overrides the production DB with an in-memory EF database.
`TestAuthHandler` provides a fake JWT scheme (`TestScheme`) so tests can authenticate without real tokens.
Integration tests in `EventControllerTests.cs` cover auth enforcement, CRUD, filtering, and user data isolation.
 
## Testing Conventions
 
- Arrange/Act/Assert sections separated by blank lines (no comments)
- Test method naming: `MethodName_Scenario_ExpectedResult`
- Test behaviour, not implementation details — assert on outcomes, not on internal calls
 
## Data Access
 
- Use EF Core for all data access; no raw SQL unless explicitly needed
- No lazy loading — always explicit `.Include()`
- Never edit migration files manually; generate via `dotnet ef migrations add`
- Custom C# methods cannot be used inside EF Core queries (`IQueryable`) — EF cannot translate them to SQL. Materialize with `ToListAsync()` first, then apply custom logic in-memory.
- **DateTime + PostgreSQL**: Npgsql requires `Kind=Utc` for `timestamp with time zone` columns. ASP.NET Core model binding parses `DateTime` from query strings as `Kind=Unspecified`. Always normalize at the top of service methods: `param = DateTime.SpecifyKind(param, DateTimeKind.Utc)`. Same applies to `new DateTime(year, month, day)` — wrap with `SpecifyKind`.
 
## What NOT to do
 
- No business logic in controllers — belongs in services
- No try/catch in controllers — `ExceptionHandlingMiddleware` handles it
- No `.Result` / `.Wait()` on async code
- No direct `AppDbContext` access outside of services
- No manual `if (model == null) return BadRequest()` — `[ApiController]` handles null bodies automatically
 
## Pdmt.Maui Conventions

- Navigation: Shell TabBar with three tabs (`events`, `calendar`, `account`) + `login` ShellContent; push pages registered via `Routing.RegisterRoute` in `AppShell.xaml.cs`
- Singleton HTTP services: inject `IHttpClientFactory`, call `factory.CreateClient(...)` per method — never store `HttpClient` as a field
- No display logic in DTOs — use ViewModel wrappers (e.g. `EventItemViewModel` over `EventResponseDto`)
- Filters with nullable enum values: use `ItemsSource` + `SelectedItem` bound to a `record` type, not `SelectedIndex` (index binding sets value to 0 on init, breaking "все" option)
- `DatePicker` returns `DateTime` with `Kind=Unspecified` — use `DateTime.SpecifyKind(value, DateTimeKind.Utc)` before sending to PostgreSQL API
- Same for `DateTime.Date` property — it preserves `Kind`, so if the source was `Unspecified`, the result is too

## Pdmt.Client Conventions
 
- Components in PascalCase, one component per file
- Use `@inject` over constructor injection in components
- API calls only via typed HttpClient services, never raw `HttpClient`
 
## Commits

- Conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `chore:`, `docs:`, `style:`, `perf:`, `ci:`, `build:`, `revert:`
- One logical change per commit
- Message: single concise line, imperative mood, no period at the end (e.g. `feat: add tag filtering`)
- Multiple types in one commit: join on the summary line with `; ` + newline per entry:
  ```
  feat: add weekly calendar
  fix: DateTime Kind in analytics
  ```
- No co-authorship lines — do not add `Co-Authored-By` to commits