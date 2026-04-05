# CLAUDE.md
 
This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.
 
## Build & Run
 
```bash
# Build entire solution (.NET only)
dotnet build Pdmt.slnx
 
# Run API (development)
dotnet run --project Pdmt.Api
 
# Run Blazor WASM client
dotnet run --project Pdmt.Client
 
# Run all tests
dotnet test Pdmt.Api.Tests/Pdmt.Api.Tests.csproj
 
# Run a single test by name
dotnet test Pdmt.Api.Tests/Pdmt.Api.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
 
# Start dev infrastructure (PostgreSQL + Redis + Seq)
docker compose up -d

# Add a new EF migration
dotnet ef migrations add <MigrationName> --project Pdmt.Api

# Apply EF migrations
dotnet ef database update --project Pdmt.Api

# Build MAUI project (Pdmt.slnx is not supported by msbuild directly)
dotnet build Pdmt.Maui/Pdmt.Maui.csproj

# Run React SPA dev server (from pdmt-web/)
npm run dev   # https://localhost:5173 (HTTPS via @vitejs/plugin-basic-ssl)
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
 
**Pdmt** is a personal event tracking app. Users log events (positive/negative experiences) with metadata: type, intensity, context, tags, and influenceability.
 
### Projects
 
- **Pdmt.Api** — ASP.NET Core 8 REST API (backend)
- **Pdmt.Api.Tests** — xUnit tests using `Microsoft.AspNetCore.Mvc.Testing` with an in-memory EF database
- **Pdmt.Client** — Blazor WebAssembly frontend with MudBlazor UI (test UI, not production)
- **Pdmt.Maui** — .NET MAUI Android client
- **pdmt-web** — React 18 + TypeScript + Vite SPA (production web frontend); located at `pdmt-web/` in solution root, not part of `.slnx`
 
### API Layers
 
- **Controllers** → **Services** → **AppDbContext** (EF Core)
- Services (`AuthService`, `EventService`, `TagService`, `AnalyticsService`) contain all business logic; controllers are thin
- Controllers: `AuthController`, `WebAuthController`, `EventsController`, `TagsController`, `AnalyticsController`
- `AnalyticsController` routes: `/weekly-summary`, `/trends`, `/correlations`, `/calendar/week`, `/calendar/month`, `/insights/*` (repeating-triggers, discounted-positives, next-day-effects, tag-combos, tag-trend, influenceability) — TODO: split insights into separate `InsightsController`
- Add `[ProducesResponseType]` and response code attributes to action methods for Swagger documentation
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
- **Two auth endpoint groups** — same `IAuthService`, different response contracts:
  - `AuthController` (`/api/auth/*`) — MAUI / Blazor: returns `AuthResultDto` with `refreshToken` in body
  - `WebAuthController` (`/api/auth/web/*`) — React SPA: returns `WebAuthResultDto` (no `refreshToken`), sets httpOnly cookie (`SameSite=None; Secure`)
- CORS policy `"WebClients"` covers all browser origins with `AllowCredentials()` — required for cookie to pass on cross-origin requests
 
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
- **DateTime/DateTimeOffset**: Use `DateTimeOffset` in all new code. When encountering legacy `DateTime`, propose migration if feasible.
- **Normalization at module boundaries**: Normalize all inbound parameters to UTC at the earliest point (controllers, clients) using `.ToUniversalTime()` before passing to services. PostgreSQL requires UTC offset only (rejects non-UTC offsets).
 
## What NOT to do
 
- No business logic in controllers — belongs in services
- No try/catch in controllers — `ExceptionHandlingMiddleware` handles it
- No `.Result` / `.Wait()` on async code
- No direct `AppDbContext` access outside of services
- No manual `if (model == null) return BadRequest()` — `[ApiController]` handles null bodies automatically
 
## Pdmt.Maui Conventions

- Navigation: Shell TabBar with four tabs (`events`, `calendar`, `insights`, `account`) + `login` ShellContent; push pages registered via `Routing.RegisterRoute` in `AppShell.xaml.cs`
- Services: `AuthService`, `EventService`, `TagService`, `AnalyticsService`, `InsightsService`
- Singleton HTTP services: inject `IHttpClientFactory`, call `factory.CreateClient(...)` per method — never store `HttpClient` as a field
- No display logic in DTOs — use ViewModel wrappers (e.g. `EventItemViewModel` over `EventResponseDto`)
- Filters with nullable enum values: use `ItemsSource` + `SelectedItem` bound to a `record` type, not `SelectedIndex` (index binding sets value to 0 on init, breaking "все" option)
- `DatePicker` returns `DateTime` with `Kind=Unspecified` — use `DateTime.SpecifyKind(value, DateTimeKind.Utc)` before sending to PostgreSQL API
- Same for `DateTime.Date` property — it preserves `Kind`, so if the source was `Unspecified`, the result is too
- Heterogeneous `CarouselView`: use `DataTemplateSelector` (see `InsightCardTemplateSelector`) — subclass, expose one `DataTemplate` property per card type, dispatch via pattern-matching `switch`
- **Insights loading lifecycle**: `InsightsViewModel` owns `CancellationTokenSource`; cards 0–1 load with priority, remaining 8 in background; `OnDisappearing` calls `CancelLoad()` to abort all in-flight HTTP requests before ViewModel is collected

## Pdmt.Client Conventions
 
- Components in PascalCase, one component per file
- Use `@inject` over constructor injection in components
- API calls only via typed HttpClient services, never raw `HttpClient`
 
## pdmt-web Conventions (React SPA)

- **Stack**: React 18 + TypeScript + Vite + Tailwind CSS + Shadcn/ui + React Router v6
- **Config**: `src/config.ts` reads `VITE_PDMT_API_BASE_URL` env var (required in prod); dev defaults to `https://localhost:7031`; set via `.env` file (see `.env.example`)
- **Auth**: `accessToken` in memory (React Context + `useRef`); `refreshToken` in httpOnly cookie — never in `localStorage`
- **API client**: `src/api/client.ts` — `apiFetch` wrapper with Bearer header injection + 401 → silent refresh → retry
- **Silent refresh on page load**: `AuthProvider` calls `POST /api/auth/web/refresh` on mount to restore session from cookie
- **All API requests**: use `credentials: 'include'` (required for cookie to be sent cross-origin)
- **Dates**: always `Date.toISOString()` before sending to API (UTC Z-suffix); `datetime-local` inputs are local time — `new Date(value).toISOString()` converts correctly
- **Tag filter**: `GET /api/events?tags=` accepts comma-separated **Guid IDs**, not names
- **Dev HTTPS**: Vite runs on `https://localhost:5173` via `@vitejs/plugin-basic-ssl` — no proxy, real cross-origin like prod
- Pages in `src/pages/`, shared components in `src/components/`, API modules in `src/api/`, auth in `src/auth/`

## Commits

- Conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `chore:`, `docs:`, `style:`, `perf:`, `ci:`, `build:`, `revert:`
- One logical change per commit
- Message: single concise line, imperative mood, no period at the end
- Multiple types in one commit: join with `; ` on the summary line
- No co-authorship lines — do not add `Co-Authored-By` to commits