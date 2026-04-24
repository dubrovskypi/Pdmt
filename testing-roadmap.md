# Integration Testing Roadmap

## Текущее состояние

Два тестовых проекта:

- **Pdmt.Api.Unit.Tests** — xUnit v3 + Moq + FluentAssertions. Покрывает контроллеры, middleware, сервисы через моки. Хорошее состояние.
- **Pdmt.Api.Integration.Tests** — xUnit v3 + InMemory EF + `WebApplicationFactory`. Покрывает как HTTP-уровень (контроллеры), так и сервисный уровень напрямую. Требует полного пересмотра.

---

## Найденные проблемы

### Архитектурные

1. **InMemory EF во всех интеграционных тестах** — не воспроизводит PostgreSQL-поведение (`AtTimeZone`, FK constraints, индексы, `DateTimeOffset` mapping через Npgsql). Тесты проходят → в продакшне могут падать.

2. **Все сервисные тесты написаны на InMemory** — `AnalyticsServiceTests`, `InsightsServiceTests`, `EventServiceTests`, `TagServiceTests`, `AuthServiceTests`. Нужно конвертировать на Testcontainers. Логику тестов не переписывать — только заменить инфраструктуру.

3. **`WebAuthWebAppFactory` наследует `CustomWebAppFactory`** — при замене фабрики цепочка наследования ломается.

### Консистентность

4. **Разные assertion-библиотеки** — юнит-тесты используют FluentAssertions, интеграционные — `Assert.*` (xUnit native). Нет единого стиля.

5. **`#region` отсутствует в юнит-тестах** — в интеграционных тестах тесты корректно сгруппированы по методу через `#region MethodName`. Юнит-тесты этого не делают — упущение, которое нужно исправить при следующем касании файлов.

6. **Разные стратегии изоляции** — `EventServiceTests` создаёт новую InMemory БД через `Guid.NewGuid()`, контроллерные тесты шарят одну БД. Нет единого подхода.

7. **Seeding размазан** — часть в конструкторе/`InitializeAsync`, часть внутри тестов. Нет понимания что уже в БД до старта теста.

8. **Именование** — интеграционные тесты используют `[Verb][Subject]_[Condition]_[ExpectedResult]` (например, `GetWeeklySummaryAsync_NoEvents_ReturnsZeroedSummary`), юнит-тесты — `[Subject]_[Condition]_[ExpectedResult]` (например, `GetEvents_NoFilter_Returns200AndCallsService`). Нужно единое соглашение.

### Инфраструктурные

9. **`Environment.SetEnvironmentVariable` в конструкторе `CustomWebAppFactory`** — побочный эффект на уровне процесса, может влиять на параллельные тест-раны.

10. **`TestDatabaseCleaner` с raw SQL** — нарушает конвенцию проекта (EF для всего доступа к данным). Имена таблиц в строках — безмолвно ломается при переименовании в миграции.

11. **Нет `FluentAssertions` в `.csproj` интеграционных тестов** — пакет не подключён.

---

## Целевая стратегия

Два уровня интеграционных тестов в этом проекте:

**HTTP-уровень** — поднимает реальный `WebHost` через `WebApplicationFactory`, отправляет запросы через `HttpClient`. Проходит весь стек: Middleware → Controller → Service → EF Core → PostgreSQL. Используется для HTTP-контрактов, auth-flow, изоляции данных.

**Сервисный уровень** — инстанциирует сервис напрямую с реальным `AppDbContext` и PostgreSQL (без WebHost). Проверяет контракт сервиса: данные на входе → правильное состояние БД → правильный результат. При падении сигнал точный: сломалось в сервисе или ниже, без необходимости разбираться в HTTP-слое.

Оба уровня существуют независимо. HTTP-тест и сервисный тест проверяют разные границы — это не дублирование, а тестирование на разных уровнях абстракции:

```
HTTP-тест упал, сервисный прошёл  → сломалось в HTTP-слое (роутинг, биндинг, middleware)
Оба упали                          → сломалось в сервисе или БД
Сервисный упал, HTTP прошёл        → такого не бывает (HTTP идёт через сервис)
```

**Что тестируем на HTTP-уровне:**
- HTTP-контракты (статус-коды, тела ответов, заголовки, Location header)
- Аутентификация и авторизация через реальный пайплайн
- Маппинг исключений в HTTP-коды через `ExceptionHandlingMiddleware`
- Сквозные сценарии (создал → получил → удалил)

**Что тестируем на сервисном уровне:**
- Сервис берёт данные и правильно сохраняет в БД
- Сложные расчёты Analytics/Insights с `AtTimeZone`
- `TagService.UpsertTagAsync` — идемпотентность по уникальному ограничению БД
- `AuthService` — refresh token rotation, инвалидация токенов в БД
- Фильтрация событий по всем параметрам (`EventService`)

**Что НЕ тестируем интеграционно (покрыто юнит-тестами):**
- Алгоритмы rate limiting
- Логика хэширования паролей, генерации JWT
- Маппинг DTO ↔ domain
- Middleware (уже в юнит-тестах: `ExceptionHandlingMiddlewareTests`, `CorrelationIdMiddlewareTests`)

---

## Целевая структура

```
Pdmt.Api.Integration.Tests/
├── Infrastructure/
│   ├── PostgresWebAppFactory.cs      # фабрика для HTTP-тестов
│   ├── TestAuthHandler.cs            # без изменений
│   ├── TestDatabaseCleaner.cs        # через EF bulk delete
│   ├── HttpTestBase.cs               # базовый класс для HTTP-тестов
│   ├── ServiceTestBase.cs            # базовый класс для сервисных тестов
│   └── Builders/
│       ├── EventBuilder.cs
│       ├── TagBuilder.cs
│       └── UserBuilder.cs
├── Controllers/
│   ├── EventControllerTests.cs       # мигрировать на PostgresWebAppFactory
│   ├── AuthControllerTests.cs        # новый
│   ├── WebAuthControllerTests.cs     # новый
│   ├── TagsControllerTests.cs        # новый
│   ├── AnalyticsControllerTests.cs   # мигрировать
│   └── InsightsControllerTests.cs    # мигрировать
├── Services/
│   ├── EventServiceTests.cs          # конвертировать с InMemory на Testcontainers
│   ├── AnalyticsServiceTests.cs      # конвертировать
│   ├── InsightsServiceTests.cs       # конвертировать
│   ├── TagServiceTests.cs            # конвертировать
│   └── AuthServiceTests.cs          # конвертировать (только token rotation, не rate limiting)
└── Pdmt.Api.Integration.Tests.csproj
```

**Удаляются:**
- `CustomWebAppFactory.cs`
- `WebAuthWebAppFactory.cs`

---

## Единые соглашения (обязательны для всех тестов)

| Аспект | Правило |
|--------|---------|
| Assertions | FluentAssertions во всех тестах (и unit, и integration) |
| Именование | `Subject_Scenario_ExpectedOutcome` |
| `#region` | Использовать: каждая группа тестов одного метода оборачивается в `#region MethodName` |
| Изоляция | `IAsyncLifetime` + `TestDatabaseCleaner` в каждом тест-классе |
| Seeding | Только в `InitializeAsync` через `HttpTestBase` / `ServiceTestBase` + builders |
| Аутентификация | `TestScheme` для обычных тестов; реальный JWT только для тестов auth-flow |

---

## Phases

---

### Phase 0 — Cleanup

**Цель:** убрать старые фабрики до начала строительства новой инфраструктуры.

- [ ] Удалить `CustomWebAppFactory.cs`
- [ ] Удалить `WebAuthWebAppFactory.cs`
- [ ] Удалить `Microsoft.EntityFrameworkCore.InMemory` из `.csproj` — после того как все тесты переведены на Testcontainers (Phase 1–5)

---

### Phase 1 — Infrastructure

**Цель:** единая инфраструктура, единый стиль.

#### 1.1 Пакеты

```xml
<!-- Pdmt.Api.Integration.Tests.csproj -->
<PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
<PackageReference Include="FluentAssertions" Version="6.12.2" />
<!-- Удалить: Microsoft.EntityFrameworkCore.InMemory -->
```

#### 1.2 `PostgresWebAppFactory`

Единственная фабрика. Реализует `IAsyncLifetime` — контейнер живёт на время жизни фабрики (один контейнер на тест-класс через `IClassFixture`).

```csharp
public sealed class PostgresWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting вместо Environment.SetEnvironmentVariable — нет побочных эффектов на процесс
        builder.UseSetting("Jwt:Secret", "test-super-secret-key-min-32-chars!!");
        builder.UseSetting("Jwt:Issuer", "pdmt-test");
        builder.UseSetting("Jwt:Audience", "pdmt-test");
        builder.UseSetting("Jwt:TokenLifetimeMinutes", "60");
        builder.UseSetting("Jwt:RefreshTokenLifetimeDays", "1");
        builder.UseSetting("Cors:AllowedOrigins:0", "https://localhost:5173");
        builder.UseSetting("App:DefaultTimeZone", "Europe/Vilnius");

        builder.ConfigureServices(services =>
        {
            services.RemoveService<DbContextOptions<AppDbContext>>();
            services.RemoveService<AppDbContext>();
            services.AddDbContext<AppDbContext>(o =>
                o.UseNpgsql(_postgres.GetConnectionString()));

            // Отключаем Redis — не нужен для тестов API
            services.RemoveService<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false"));

            // Dual-auth: TestScheme для большинства тестов, реальный JWT для auth-flow тестов
            services.AddAuthentication(o =>
            {
                o.DefaultScheme = "TestOrJwt";
                o.DefaultAuthenticateScheme = "TestOrJwt";
                o.DefaultChallengeScheme = "TestOrJwt";
            })
            .AddPolicyScheme("TestOrJwt", null, o =>
            {
                o.ForwardDefaultSelector = ctx =>
                {
                    var header = ctx.Request.Headers[HeaderNames.Authorization]
                        .FirstOrDefault();
                    return header?.StartsWith("TestScheme") == true
                        ? TestAuthHandler.SchemeName
                        : JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { })
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("test-super-secret-key-min-32-chars!!"))
                };
            });

            // Отключаем rate limiting для большинства тестов
            services.RemoveService<IRateLimitService>();
            services.AddSingleton<IRateLimitService, NoOpRateLimitService>();
        });
    }
}
```

> **Примечание:** тесты auth с rate limiting (если понадобятся) создают отдельный клиент с реальным `IRateLimitService` через `WithWebHostBuilder` override.

#### 1.3 `TestDatabaseCleaner`

Типобезопасная очистка через EF bulk delete (EF 7+):

```csharp
public static class TestDatabaseCleaner
{
    // Порядок важен: дочерние сущности раньше родительских (FK)
    public static async Task CleanAsync(AppDbContext db)
    {
        await db.EventTags.ExecuteDeleteAsync();
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.FailedLoginAttempts.ExecuteDeleteAsync();
        await db.Events.ExecuteDeleteAsync();
        await db.Tags.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
    }
}
```

#### 1.4 `HttpTestBase`

Базовый класс для HTTP-тестов — единственное место для seeding тестового пользователя и настройки `HttpClient`:

```csharp
public abstract class HttpTestBase : IClassFixture<PostgresWebAppFactory>, IAsyncLifetime
{
    protected readonly PostgresWebAppFactory Factory;
    protected HttpClient Client;

    protected HttpTestBase(PostgresWebAppFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("TestScheme");
    }

    public virtual async Task InitializeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(); // верифицирует реальные миграции
        await TestDatabaseCleaner.CleanAsync(db);
        await SeedDefaultUserAsync(db);
    }

    public virtual Task DisposeAsync() => Task.CompletedTask;

    protected static async Task SeedDefaultUserAsync(AppDbContext db)
    {
        db.Users.Add(new User
        {
            Id = TestAuthHandler.TestUserId,
            Email = "test@pdmt.dev",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
        });
        await db.SaveChangesAsync();
    }
}
```

#### 1.5 `ServiceTestBase`

Базовый класс для сервисных тестов — без WebHost, реальный `AppDbContext` напрямую:

```csharp
public abstract class ServiceTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    protected AppDbContext Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        Db = new AppDbContext(options);
        await Db.Database.MigrateAsync();
        await TestDatabaseCleaner.CleanAsync(Db);
        await SeedDefaultUserAsync(Db);
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected static async Task SeedDefaultUserAsync(AppDbContext db)
    {
        db.Users.Add(new User
        {
            Id = TestAuthHandler.TestUserId,
            Email = "test@pdmt.dev",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
        });
        await db.SaveChangesAsync();
    }
}
```

> **Примечание:** каждый тест-класс поднимает свой контейнер. Если тест-сьют вырастет, можно перейти на `ICollectionFixture<PostgreSqlContainer>` для шаринга контейнера между классами — но оптимизировать рано.

#### 1.6 Builders

```csharp
// Builders/EventBuilder.cs
public sealed class EventBuilder
{
    private Guid _userId = TestAuthHandler.TestUserId;
    private EventType _type = EventType.Positive;
    private int _intensity = 5;
    private DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
    private string? _description;

    public EventBuilder WithUserId(Guid userId) { _userId = userId; return this; }
    public EventBuilder WithType(EventType type) { _type = type; return this; }
    public EventBuilder WithIntensity(int intensity) { _intensity = intensity; return this; }
    public EventBuilder WithTimestamp(DateTimeOffset ts) { _timestamp = ts; return this; }
    public EventBuilder WithDescription(string description) { _description = description; return this; }

    public Event Build() => new()
    {
        Id = Guid.NewGuid(),
        UserId = _userId,
        Type = _type,
        Intensity = _intensity,
        Timestamp = _timestamp,
        Description = _description
    };
}
```

---

### Phase 2 — Migrate EventControllerTests

**Цель:** рабочий эталонный тест-класс на новой инфраструктуре.

- [ ] Заменить `IClassFixture<CustomWebAppFactory>` → унаследовать `HttpTestBase`
- [ ] Удалить `IAsyncLifetime` из класса (переехал в базовый)
- [ ] Заменить все `Assert.*` на FluentAssertions
- [ ] Переименовать методы под единое соглашение `Subject_Scenario_ExpectedOutcome`
- [ ] Убедиться что тесты проходят на реальном PostgreSQL

**Чеклист сценариев для `EventControllerTests`:**

| Сценарий | Метод |
|----------|-------|
| GET без авторизации | `GetEvents_Unauthenticated_Returns401` |
| GET с TestScheme | `GetEvents_Authenticated_Returns200` |
| GET с реальным JWT | `GetEvents_ValidJwt_Returns200` |
| POST happy path | `CreateEvent_ValidRequest_Returns201WithLocation` |
| POST — данные в БД | `CreateEvent_ValidRequest_PersistsToDatabase` |
| POST с тегами | `CreateEvent_WithTags_ReturnsTagsInResponse` |
| POST — intensity вне диапазона | `CreateEvent_IntensityOutOfRange_Returns400` |
| GET /{id} — своё событие | `GetEvent_OwnEvent_Returns200` |
| GET /{id} — чужое событие | `GetEvent_OtherUsersEvent_Returns404` |
| GET /{id} — не существует | `GetEvent_NotFound_Returns404` |
| PUT happy path | `UpdateEvent_OwnEvent_Returns204` |
| PUT — чужое событие | `UpdateEvent_OtherUsersEvent_Returns404` |
| DELETE happy path | `DeleteEvent_OwnEvent_Returns204ThenGet404` |
| Фильтр по типу | `GetEvents_FilterByType_ReturnsMatchingOnly` |
| Фильтр по дате | `GetEvents_FilterByDateRange_ReturnsMatchingOnly` |
| Фильтр по тегам | `GetEvents_FilterByTagIds_ReturnsMatchingOnly` |
| Изоляция по userId | `GetEvents_OtherUsersEventsExist_NotIncluded` |

---

### Phase 3 — Auth тесты

**Цель:** покрыть auth-flow который нельзя протестировать юнит-тестами (cookie, refresh rotation в БД).

#### `AuthControllerTests` (MAUI / Blazor flow — refresh token в теле)

| Сценарий | Метод |
|----------|-------|
| Login с верными данными | `Login_ValidCredentials_Returns200WithTokens` |
| Login с неверным паролем | `Login_WrongPassword_Returns401` |
| Login — несуществующий email | `Login_UnknownEmail_Returns401` |
| Refresh с валидным токеном | `Refresh_ValidToken_Returns200WithNewTokens` |
| Refresh с истёкшим токеном | `Refresh_ExpiredToken_Returns401` |
| Refresh — повторное использование | `Refresh_AlreadyUsedToken_Returns401` |
| Register happy path | `Register_ValidData_Returns201` |
| Register — дубликат email | `Register_DuplicateEmail_Returns400` |

> **Примечание:** эти тесты нуждаются в реальном `IRateLimitService` → создавать через отдельный `WithWebHostBuilder` без `NoOpRateLimitService`, либо вынести в отдельную фабрику, унаследованную от `PostgresWebAppFactory`.

#### `WebAuthControllerTests` (React SPA flow — cookie)

```csharp
// Клиент с поддержкой cookie — обязательно
Client = factory.CreateClient(new WebApplicationFactoryClientOptions
{
    HandleCookies = true
});
```

| Сценарий | Метод |
|----------|-------|
| Login устанавливает httpOnly cookie | `Login_ValidCredentials_SetsHttpOnlyCookie` |
| Login не возвращает refreshToken в теле | `Login_ValidCredentials_RefreshTokenNotInBody` |
| Refresh с cookie → новый access token | `Refresh_WithValidCookie_Returns200` |
| Refresh без cookie → 401 | `Refresh_WithoutCookie_Returns401` |
| Logout инвалидирует cookie | `Logout_ClearsRefreshCookie` |

---

### Phase 4 — Tags, Analytics, Insights

**Цель:** покрыть оставшиеся контроллеры.

#### `TagsControllerTests`

| Сценарий | Метод |
|----------|-------|
| Создать тег | `CreateTag_ValidName_Returns201` |
| Создать дубликат — idempotent | `CreateTag_DuplicateName_Returns200WithExistingTag` |
| Чужие теги не видны | `GetTags_OtherUsersTagsExist_NotIncluded` |
| Удалить тег | `DeleteTag_OwnTag_Returns204` |

#### `AnalyticsControllerTests`

Все тесты требуют реального PostgreSQL (используется `EF.Functions.AtTimeZone`).

| Сценарий | Метод |
|----------|-------|
| Weekly summary — нет событий | `GetWeeklySummary_NoEvents_ReturnsZeroes` |
| Weekly summary — happy path | `GetWeeklySummary_WithEvents_ReturnsCorrectCounts` |
| Calendar week — событие на границе полуночи по Вильнюсу | `GetCalendarWeek_EventAtMidnightLithuania_GroupedByLocalDay` |
| Correlations — happy path | `GetCorrelations_WithTaggedEvents_ReturnsData` |

> **Граничный тест полуночи важен:** событие в 22:00 UTC = 00:00+02:00 по Вильнюсу. InMemory это не воспроизводит — только реальный PostgreSQL.

#### `InsightsControllerTests`

Достаточно smoke-тестов на доступность эндпоинтов (логика покрыта юнит-тестами):

```csharp
[Theory]
[InlineData("/api/insights/balance")]
[InlineData("/api/insights/trends")]
[InlineData("/api/insights/most-intense-tags")]
[InlineData("/api/insights/repeating-triggers")]
[InlineData("/api/insights/weekday-stats")]
[InlineData("/api/insights/influenceability")]
public async Task InsightsEndpoint_Authenticated_Returns200(string url)
```

---

### Phase 5 — Convert service tests to Testcontainers

**Цель:** конвертировать существующие сервисные тесты с InMemory на реальный PostgreSQL. Логику тестов не переписывать — только заменить инфраструктуру.

#### Паттерн конвертации (одинаков для всех файлов)

```csharp
// До — InMemory, изоляция через Guid.NewGuid()
public class AnalyticsServiceTests
{
    private readonly AppDbContext _db;
    private readonly AnalyticsService _sut;

    public AnalyticsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new AnalyticsService(_db, ...);
    }
}

// После — Testcontainers, изоляция через TestDatabaseCleaner в ServiceTestBase
public class AnalyticsServiceTests : ServiceTestBase
{
    private AnalyticsService _sut = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync(); // поднимает контейнер, мигрирует, чистит, сидирует
        _sut = new AnalyticsService(Db, ...); // реальный AppDbContext
    }
}
```

#### Что менять в каждом файле

| Файл | Что заменить | Что сохранить |
|------|-------------|---------------|
| `EventServiceTests` | Конструктор + InMemory опции | Все тест-методы и assertions |
| `AnalyticsServiceTests` | Конструктор + InMemory опции | Все тест-методы и assertions |
| `InsightsServiceTests` | Конструктор + InMemory опции | Все тест-методы и assertions |
| `TagServiceTests` | Конструктор + InMemory опции | Все тест-методы и assertions |
| `AuthServiceTests` | Конструктор + InMemory опции; удалить тесты rate limiting (дубль `AuthServiceUnitTests`) | Token rotation, invalidation тесты |

- [ ] Конвертировать `EventServiceTests`
- [ ] Конвертировать `AnalyticsServiceTests`
- [ ] Конвертировать `InsightsServiceTests`
- [ ] Конвертировать `TagServiceTests`
- [ ] Конвертировать `AuthServiceTests` (предварительно удалить rate limiting тесты — они в `AuthServiceUnitTests`)
- [ ] Заменить `Assert.*` на FluentAssertions во всех файлах
- [ ] Привести именование к `Subject_Scenario_ExpectedOutcome`

---

### Phase 6 — Exception mapping (опционально)

Middleware уже покрыт юнит-тестами (`ExceptionHandlingMiddlewareTests`, `CorrelationIdMiddlewareTests`). Добавлять интеграционные тесты только если нужно верифицировать формат JSON-ответа на реальном стеке:

```csharp
// Достаточно одного теста на маппинг
[Fact]
public async Task ExceptionMiddleware_NotFoundException_Returns404WithJsonBody()
```

---

## Итоговый порядок выполнения

| Phase | Задача | Приоритет |
|-------|--------|-----------|
| 0 | Удалить старые фабрики (`CustomWebAppFactory`, `WebAuthWebAppFactory`) | Высокий |
| 1 | Инфраструктура: фабрика, cleaner, `HttpTestBase`, `ServiceTestBase`, builders | Высокий |
| 2 | Мигрировать `EventControllerTests` — эталонный HTTP-тест | Высокий |
| 3 | `AuthControllerTests` + `WebAuthControllerTests` | Высокий |
| 4a | `TagsControllerTests` | Средний |
| 4b | `AnalyticsControllerTests` | Средний |
| 4c | `InsightsControllerTests` (smoke) | Низкий |
| 5 | Конвертировать сервисные тесты на Testcontainers (`Event`, `Analytics`, `Insights`, `Tag`, `Auth`) | Средний |
| 6 | Удалить `InMemory` пакет из `.csproj` | Высокий (после Phase 5) |
| 7 | Exception mapping интеграционный тест | Низкий |

---

## Комментарии к найденным проблемам

Ниже — проблемы, обнаруженные при анализе, которые не вошли в исходное обсуждение:

**[P1] Дублирование покрытия rate limiting в `AuthServiceTests`**
`AuthServiceTests` содержит тесты rate limiting, которые уже есть в `AuthServiceUnitTests`. В Phase 5 при конвертации `AuthServiceTests` — эти тесты удалить, оставить только token rotation и invalidation.

**[P2] `WebAuthWebAppFactory` унаследован от `CustomWebAppFactory`**
После Phase 0 `CustomWebAppFactory` удаляется. `WebAuthWebAppFactory` перестанет компилироваться. В новой архитектуре rate limiting отключён через `NoOpRateLimitService` в `PostgresWebAppFactory` по умолчанию — `WebAuthWebAppFactory` как отдельный класс больше не нужен. WebAuth тесты используют `PostgresWebAppFactory` напрямую.

**[P3] `Environment.SetEnvironmentVariable` в `CustomWebAppFactory`**
Устанавливает переменные среды на уровне процесса. При параллельном запуске тестов (xUnit по умолчанию запускает тест-классы параллельно) это может вызывать race conditions. В новой фабрике заменено на `builder.UseSetting()`.

**[P4] Отсутствие `#region` в юнит-тестах**
Интеграционные тесты правильно группируют тесты по методу через `#region MethodName`. Юнит-тесты этого не делают — добавить при следующем касании файлов.

**[P5] Отсутствие `MigrateAsync()` в тестах**
Текущие InMemory тесты не применяют миграции — схема создаётся через `EnsureCreated()`. Это означает что корректность миграций никогда не верифицировалась тестами. `IntegrationTestBase.InitializeAsync()` вызывает `MigrateAsync()` — это закрывает пробел.

**[P6] `TestHelpers.MakeEvent()` и builders**
`TestHelpers` останется для HTTP-уровня (`MakeCreateDto()`). Для прямого seeding в БД использовать `EventBuilder` / `UserBuilder` — более гибко и читаемо. Оба подхода применимы, важно не смешивать: DTO-helpers для HTTP-запросов, builders для seed-данных.
