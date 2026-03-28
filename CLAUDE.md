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
 
# Add a new EF migration
dotnet ef migrations add <MigrationName> --project Pdmt.Api

# Apply EF migrations
dotnet ef database update --project Pdmt.Api
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
 
### API Layers
 
- **Controllers** → **Services** → **AppDbContext** (EF Core)
- Services (`AuthService`, `EventService`, `TagService`, `StatsService`, `SummaryService`, `UserService`) contain all business logic; controllers are thin
- `TokenCleanupBgService` — background service that purges expired refresh tokens
- Global exception handling via `ExceptionHandlingMiddleware` — do not add try/catch in controllers
 
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
 
- **Development:** SQL Server LocalDB (`appsettings.Development.json`)
- **Production:** PostgreSQL (`appsettings.Prod.json`)
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
 
## What NOT to do
 
- No business logic in controllers — belongs in services
- No try/catch in controllers — `ExceptionHandlingMiddleware` handles it
- No `.Result` / `.Wait()` on async code
- No direct `AppDbContext` access outside of services
 
## Pdmt.Client Conventions
 
- Components in PascalCase, one component per file
- Use `@inject` over constructor injection in components
- API calls only via typed HttpClient services, never raw `HttpClient`
 
## Commits

- Conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `chore:`, `docs:`, `style:`, `perf:`, `ci:`, `build:`, `revert:`
- One logical change per commit
- Message: single concise line, imperative mood, no period at the end (e.g. `feat: add tag filtering`)
- No co-authorship lines — do not add `Co-Authored-By` to commits