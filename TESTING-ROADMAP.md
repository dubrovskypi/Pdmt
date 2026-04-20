# Unit Testing Roadmap — Pdmt

> Документ описывает стратегию, декомпозицию и пошаговый план
> доведения покрытия бизнес-логики Pdmt до максимума.
> Актуален для состояния кодовой базы на апрель 2026.

---

## 0. Диагноз текущего состояния

### Что есть сейчас

| Файл тестов | Тип теста (фактически) | Мокается ли БД? |
|---|---|---|
| `InMemoryRateLimitServiceTests.cs` | ✅ Unit | — (нет БД) |
| `AuthServiceTests.cs` | ⚠️ Integration | Нет, InMemory EF |
| `EventServiceTests.cs` | ⚠️ Integration | Нет, InMemory EF |
| `TagServiceTests.cs` | ⚠️ Integration | Нет, InMemory EF |
| `AnalyticsServiceTests.cs` | ⚠️ Integration | Нет, InMemory EF |
| `InsightsServiceTests.cs` | ⚠️ Integration | Нет, InMemory EF |
| `EventControllerTests.cs` | ✅ Integration | CustomWebAppFactory |
| `InsightsControllerTests.cs` | ✅ Integration | CustomWebAppFactory |
| `AnalyticsControllerTests.cs` | ❌ Stub | — |

### Главная проблема

Все тесты сервисов используют `InMemory EF Core` — это **не unit-тесты**:
- InMemory база не проверяет SQL-ограничения и поведение Postgres
- Тесты медленнее, чем должны быть
- Не изолируют бизнес-логику от доступа к данным
- Дают ложную уверенность в покрытии

**Единственный настоящий unit-тест** в проекте: `InMemoryRateLimitServiceTests` —
именно потому, что `InMemoryRateLimitService` не зависит от `AppDbContext`.

---

## 1. Стратегия тестирования

### Что считается unit-тестом в данном проекте

Unit-тест — тест, который:
- Не обращается к базе данных (ни InMemory, ни реальной)
- Не поднимает HTTP-сервер
- Изолирует один класс / один метод
- Работает в памяти, занимает < 10 мс
- Использует **Moq** для замены внешних зависимостей

### Что НЕ является unit-тестом (и почему)

| Что | Почему не unit |
|---|---|
| Сервисы с InMemory EF | EF — внешняя зависимость, InMemory != мок |
| Тесты с CustomWebAppFactory | Поднимает DI-контейнер и middleware |
| Тесты с SQLite in-process | SQLite — реальная БД, тест зависит от движка |

### Граница unit / integration

```
Unit                         Integration
────────────────────         ────────────────────────────────
Чистая логика сервиса        Сервис + InMemory EF Core
Маппинг / вычисления         Сервис + Testcontainers (Postgres)
Rate limiting (память)       Контроллер + CustomWebAppFactory
Middleware (изолированно)    E2E через HTTP-клиент
```

### Целевая структура: два отдельных проекта

```
Pdmt.slnx
├── Pdmt.Api/
├── Pdmt.Api.Unit.Tests/          ← новый проект
│   ├── Services/
│   │   ├── AuthServiceUnitTests.cs
│   │   ├── InsightsComputationTests.cs
│   │   └── InMemoryRateLimitServiceTests.cs
│   ├── Controllers/
│   │   ├── AuthControllerTests.cs
│   │   ├── EventsControllerTests.cs
│   │   ├── TagsControllerTests.cs
│   │   ├── InsightsControllerTests.cs
│   │   └── AnalyticsControllerTests.cs
│   └── Middleware/
│       ├── ExceptionHandlingMiddlewareTests.cs
│       └── CorrelationIdMiddlewareTests.cs
└── Pdmt.Api.Integration.Tests/   ← переименовать существующий Pdmt.Api.Tests
    ├── Services/
    │   ├── AuthServiceTests.cs
    │   ├── EventServiceTests.cs
    │   ├── TagServiceTests.cs
    │   ├── AnalyticsServiceTests.cs
    │   └── InsightsServiceTests.cs
    └── Controllers/
        ├── EventControllerTests.cs
        ├── InsightsControllerTests.cs
        ├── AnalyticsControllerTests.cs     (сейчас пустой — реализовать)
        └── WebAuthControllerTests.cs       (отсутствует — добавить)
```

**Зависимости проектов:**

```
Pdmt.Api.Unit.Tests        →  Pdmt.Api (только исходный код)
Pdmt.Api.Integration.Tests →  Pdmt.Api + Microsoft.AspNetCore.Mvc.Testing
```

`CustomWebAppFactory` и `TestAuthHandler` остаются в Integration — Unit-проекту они не нужны.

**Запуск:**

```bash
dotnet test Pdmt.Api.Unit.Tests             # быстро, < 1 сек, без Docker
dotnet test Pdmt.Api.Integration.Tests      # медленнее, требует Docker
dotnet test                                 # всё
```

---

## 2. Архитектурные изменения, необходимые для unit-тестов

### Проблема

Сервисы принимают `AppDbContext` напрямую — его нельзя заменить моком без
сложного мокирования `DbSet<T>` (антипаттерн, хрупко, много boilerplate).

### Решение A — обязательное: интерфейсы сервисов

Ввести интерфейсы для всех сервисов — это разблокирует unit-тесты **контроллеров**.

```csharp
// Pdmt.Api/Services/IEventService.cs
public interface IEventService
{
    Task<IReadOnlyList<EventResponseDto>> GetEventsAsync(
        Guid userId, DateTimeOffset? from, DateTimeOffset? to,
        DtoEventType? type, IReadOnlyList<Guid>? tagIds,
        int? minIntensity, int? maxIntensity);
    Task<EventResponseDto?> GetByIdAsync(Guid userId, Guid id);
    Task<EventResponseDto> CreateEventAsync(Guid userId, CreateEventDto ev);
    Task<bool> UpdateEventAsync(Guid userId, Guid eventId, UpdateEventDto dto);
    Task DeleteEventAsync(Guid userId, Guid id);
}

// EventService : IEventService — без изменений в реализации
// EventsController(IEventService svc) — вместо EventService
// DI: services.AddScoped<IEventService, EventService>();
```

Аналогично для `IAuthService`, `ITagService`, `IAnalyticsService`, `IInsightsService`.

### Решение B — рекомендуемое: вынести вычислительную логику

`InsightsService` и `AnalyticsService` **материализуют данные из БД**, затем
**обрабатывают их в памяти**. Эту обработку можно вынести в `internal static`
методы без зависимости от EF — и тестировать как чистые функции.

```csharp
// До рефакторинга
public async Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(...)
{
    var events = await db.Events
        .Where(e => e.UserId == userId && ...)
        .Include(e => e.EventTags).ThenInclude(et => et.Tag)
        .ToListAsync();
    // ... вычисления ...
}

// После рефакторинга
public async Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(...)
{
    var events = await db.Events
        .Where(e => e.UserId == userId && ...)
        .Include(e => e.EventTags).ThenInclude(et => et.Tag)
        .ToListAsync();
    return ComputeRepeatingTriggers(events, minCount); // ← unit-тестируемый
}

internal static IReadOnlyList<RepeatingTriggerDto> ComputeRepeatingTriggers(
    IReadOnlyList<Event> events, int minCount)
{
    return events
        .Where(e => e.Type == EventType.Negative)
        .SelectMany(e => e.EventTags.Select(et => et.Tag))
        .GroupBy(t => t.Id)
        .Where(g => g.Count() >= minCount)
        .Select(g => new RepeatingTriggerDto { ... })
        .OrderByDescending(t => t.Count)
        .ToList();
}
```

---

## 3. Декомпозиция по слоям

### 3.1 Controllers

**Цель unit-тестов контроллеров:** HTTP-коды, маппинг результатов, routing.
Не бизнес-логика — она в сервисах.

**Что тестировать:**
- Метод вызывает нужный метод сервиса с правильными параметрами
- Возвращает правильный HTTP-статус (200/201/204/400/401/404)
- `CreatedAtAction` содержит корректный Location header
- Обработка `null`-результата от сервиса → 404

**Что НЕ тестировать в unit-тестах контроллеров:**
- Бизнес-логику (это в интеграционных тестах сервисов)
- Middleware (тестируется отдельно)
- Реальную JWT-валидацию

**Матрица тест-кейсов контроллеров:**

```
EventsController:
  GetEvents    → сервис вызван с корректными параметрами фильтра → 200
  GetEvent     → null от сервиса → 404; нашёл → 200
  CreateEvent  → возвращает 201 + Location header
  UpdateEvent  → false от сервиса → 404; true → 204
  DeleteEvent  → 204

AuthController:
  Register     → 201, тело содержит AccessToken и RefreshToken
  Login        → 200, тело содержит токены
  Refresh      → 200, тело содержит токены
  Logout       → 204

TagsController:
  GetTags      → 200, возвращает список
  UpsertTag    → 200, возвращает TagResponseDto
  DeleteTag    → false → 404; true → 204

InsightsController / AnalyticsController:
  Все GET      → 200 с корректным телом
  from > to    → 400 (валидация параметров — уже в integration, но unit проверяет binding)
```

### 3.2 Services

**Ключевой принцип:** сервисные методы делятся на два вида:

1. **Orchestration** (вызов БД + вызов вычислений) → Integration тест
2. **Computation** (чистая логика над List\<T\>) → Unit тест

#### AuthService — что тестировать как unit

`IRateLimitService` уже мокается — AuthService частично поддаётся unit-тестированию:

```
RegisterAsync:
  ✅ Rate limit exceeded → RateLimitExceededException
  ✅ Дублирующийся email → InvalidOperationException (integration: с InMemory)
  ✅ Email нормализуется (uppercase) перед сохранением
  ✅ RefreshToken в БД — хэш, не plaintext (integration)

LoginAsync:
  ✅ Rate limit exceeded → RateLimitExceededException
  ✅ Неверный пароль → UnauthorizedAccessException (integration)
  ✅ Несуществующий email → UnauthorizedAccessException (integration)

RefreshAsync:
  ✅ Rate limit exceeded → RateLimitExceededException
```

#### InsightsService — полный список Compute-методов для unit

| Метод сервиса | Compute-метод (unit) |
|---|---|
| GetRepeatingTriggersAsync | `ComputeRepeatingTriggers(events, minCount)` |
| GetDiscountedPositivesAsync | `ComputeDiscountedPositives(events)` |
| GetNextDayEffectsAsync | `ComputeNextDayEffects(events)` |
| GetTagCombosAsync | `ComputeTagCombos(events)` |
| GetInfluenceabilitySplitAsync | `ComputeInfluenceabilitySplit(events)` |
| GetBalanceAsync | `ComputeBalance(events)` |
| GetWeekdayStatsAsync | `ComputeWeekdayStats(events)` |

**Тест-кейсы для каждого Compute-метода:**

```
ComputeRepeatingTriggers:
  ✅ Тег встречается >= minCount раз → включается
  ✅ Тег встречается < minCount раз → исключается
  ✅ Учитываются только негативные события
  ✅ Сортировка: по убыванию count
  ✅ Нет событий → пустой список (не исключение)

ComputeDiscountedPositives:
  ✅ 5+ событий и avg < 4.0 → включается
  ✅ < 5 событий → исключается
  ✅ avg == 4.0 → исключается (граничное значение)
  ✅ avg > 4.0 → исключается
  ✅ Только позитивные события учитываются

ComputeNextDayEffects:
  ✅ Тег на 3+ разных днях → включается
  ✅ Тег на 2 днях → исключается
  ✅ Сортировка: по |score| убыванию
  ✅ Нет следующего дня для последнего дня → не ломается

ComputeTagCombos:
  ✅ Пара встречается 3+ раз в один день → включается
  ✅ Пара встречается 2 раза → исключается
  ✅ aloneIntensity < combinedIntensity → difference положительный
  ✅ Нет пар → пустой список

ComputeInfluenceabilitySplit:
  ✅ Только негативные события учитываются
  ✅ canInfluence / cannotInfluence разделяются правильно
  ✅ Нет негативных событий → нули (не NaN, не деление на 0)
  ✅ Все события canInfluence → cannotInfluenceAvg = 0

ComputeBalance:
  ✅ Нет событий → нули
  ✅ Только позитивные → ratio = 0 (нет негативных для деления)
  ✅ Соотношение рассчитывается корректно

ComputeWeekdayStats:
  ✅ Всегда возвращает ровно 7 элементов (пн–вс)
  ✅ Порядок: понедельник первый, воскресенье последнее
  ✅ Пустые дни → avg = 0, count = 0 (не ошибка)
```

#### TagService:

```
UpsertTagAsync (integration — нужна БД):
  ✅ Новое имя → создаётся
  ✅ Существующее имя (case-sensitive) → переиспользуется
  ✅ То же имя с пробелами → трим → находит существующий
  ✅ То же имя у другого пользователя → создаёт новый

DeleteTagAsync (integration):
  ✅ Существующий тег → true
  ✅ Несуществующий → false
  ✅ Чужой тег → false
```

### 3.3 Repositories

В проекте нет репозиториев — прямой доступ через `AppDbContext`.

**Вывод:** вводить репозитории только ради тестирования — over-engineering.
SQL/EF-запросы тестируются **только в integration-тестах** (InMemory или Testcontainers).

### 3.4 Middleware

**ExceptionHandlingMiddleware — обязательные unit-тесты:**

```
✅ RateLimitExceededException → 429
✅ NotFoundException → 404
✅ UnauthorizedAccessException → 401
✅ InvalidOperationException → 400
✅ Exception (generic) → 500
✅ Development: details содержат stack trace (не null)
✅ Production: details = null
✅ CorrelationId присутствует в ответе
✅ Content-Type = application/json
```

**CorrelationIdMiddleware:**

```
✅ Заголовок X-Correlation-Id отсутствует → генерирует новый GUID
✅ Заголовок X-Correlation-Id передан → использует его, не перезаписывает
✅ GUID добавляется в response headers
```

---

## 4. Работа с EF Core

### Правило разделения

```
Метод делает IQueryable / await db.*  →  Integration тест (InMemory или Testcontainers)
Метод принимает List<T> / IReadOnlyList<T>  →  Unit тест (чистая функция)
```

### Когда что использовать

| Подход | Когда | Рекомендация |
|---|---|---|
| Мокать `DbSet<T>` через Moq | Изолировать DbContext | ❌ Хрупко, не используй |
| InMemory EF Core | Тесты query-логики | ✅ Integration только |
| SQLite in-process | Ближе к Postgres, без Docker | ⚠️ Integration, не unit |
| Testcontainers (Postgres) | Точная копия прода | ✅ CI/CD integration |
| Вынести вычисления | Тест без БД | ✅ Приоритет для unit |

### Когда стоит перейти на Testcontainers

Когда InMemory начинает скрывать баги:
- Нарушения constraint (unique, FK)
- Поведение `EF.Functions.AtTimeZone()`
- Специфичные Postgres-функции

Сейчас `AnalyticsService` использует `EF.Functions.AtTimeZone` — InMemory это не
транслирует. Эти тесты кандидаты на Testcontainers.

---

## 5. Метрики покрытия

### Инструменты

```bash
# Установить в оба тестовых проекта
dotnet add Pdmt.Api.Unit.Tests package coverlet.collector
dotnet add Pdmt.Api.Integration.Tests package coverlet.collector

# Запуск с покрытием (оба проекта, результаты в одну папку)
dotnet test Pdmt.Api.Unit.Tests --collect:"XPlat Code Coverage" --results-directory ./coverage
dotnet test Pdmt.Api.Integration.Tests --collect:"XPlat Code Coverage" --results-directory ./coverage

# Объединённый HTML-отчёт по обоим проектам
reportgenerator \
  -reports:"coverage/**/coverage.cobertura.xml" \
  -targetdir:"coverage/report" \
  -reporttypes:"Html;Badges"
```

### Целевые метрики

| Зона | Line Coverage | Branch Coverage |
|---|---|---|
| Middleware | 100% | 100% |
| Controllers (unit) | 95% | 90% |
| Вычислительная логика (Compute-методы) | 100% | 95% |
| Rate limiting | 100% | 95% |
| Сервисы (integration) | 85% | 80% |
| **Общий минимум** | **80%** | **75%** |

### Зоны обязательного 100% покрытия

1. `ExceptionHandlingMiddleware` — все ветки маппинга исключений
2. `InMemoryRateLimitService` — все условия лимитирования
3. Все `Compute*` методы InsightsService после рефакторинга
4. `EventService.MapToResponseDto` — маппинг

---

## 6. Best Practices

### Именование тестов

```
[MethodName]_[Scenario]_[ExpectedResult]

Примеры:
  RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException
  GetEventsAsync_FilterByType_ReturnsOnlyMatchingEvents
  InvokeAsync_NotFoundException_Returns404
  ComputeRepeatingTriggers_BelowMinCount_ExcludesTag
  ComputeInfluenceabilitySplit_NoNegativeEvents_ReturnsZeros
```

### Структура теста (AAA)

```csharp
[Fact]
public async Task GetEvent_EventNotFound_Returns404()
{
    // Arrange
    _eventService
        .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
        .ReturnsAsync((EventResponseDto?)null);

    // Act
    var result = await _sut.GetEvent(Guid.NewGuid());

    // Assert
    result.Should().BeOfType<NotFoundResult>();
}
```

Правила:
- Секции разделены пустой строкой
- Нет комментариев `// Arrange` — пустая строка и так читается
- Одна вещь на тест, один assert (или связанный набор для одного объекта)

### NuGet-пакеты по проектам

```xml
<!-- Pdmt.Api.Unit.Tests.csproj -->
<PackageReference Include="FluentAssertions" Version="6.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="AutoFixture" Version="4.*" />
<PackageReference Include="AutoFixture.AutoMoq" Version="4.*" />
<PackageReference Include="coverlet.collector" Version="6.*" />
<!-- Microsoft.AspNetCore.Mvc.Testing НЕ нужен -->

<!-- Pdmt.Api.Integration.Tests.csproj (существующий Pdmt.Api.Tests) -->
<PackageReference Include="FluentAssertions" Version="6.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.*" />
<PackageReference Include="coverlet.collector" Version="6.*" />
```

### Изоляция тестов

```csharp
// ✅ Хорошо: каждый тест создаёт свой мок
public class EventsControllerTests
{
    private readonly Mock<IEventService> _eventService = new();
    private readonly EventsController _sut;

    public EventsControllerTests()
    {
        _sut = new EventsController(_eventService.Object);
        _sut.ControllerContext = BuildControllerContext(Guid.NewGuid());
    }
}

// ❌ Плохо: статический мок, переиспользуется между тестами
private static readonly Mock<IEventService> _sharedMock = new();
```

### Избегать over-mocking

```csharp
// ❌ Мокаем то, что должно работать реально
var mockMapper = new Mock<IMapper>();
mockMapper.Setup(m => m.Map<EventResponseDto>(It.IsAny<Event>()))
          .Returns(new EventResponseDto()); // теряем реальный маппинг

// ✅ Мокаем только внешние зависимости / границы системы
var mockRateLimit = new Mock<IRateLimitService>();
mockRateLimit
    .Setup(r => r.CheckAsync(It.IsAny<string>(), It.IsAny<string>()))
    .Returns(Task.CompletedTask);
var sut = new AuthService(db, config, mockRateLimit.Object);
```

### AutoFixture — когда уместен

```csharp
// ✅ Уместно: DTO с произвольными валидными данными
var fixture = new Fixture();
var dto = fixture.Build<CreateEventDto>()
                 .With(x => x.Intensity, 5)      // фиксируем важное поле
                 .With(x => x.Type, DtoEventType.Positive)
                 .Create();

// ❌ Не уместно: конкретные значения влияют на логику теста
// В этом случае создавай вручную для ясности
```

---

## 7. Пошаговый план действий

### Шаг 1 — Создать проекты и перенести тесты (1 день)

1. Переименовать `Pdmt.Api.Tests` → `Pdmt.Api.Integration.Tests` (папка + `.csproj` + namespace)
2. Обновить `Pdmt.slnx`: заменить ссылку на старый проект новой
3. Создать новый проект `Pdmt.Api.Unit.Tests`:
   ```bash
   dotnet new xunit -n Pdmt.Api.Unit.Tests
   dotnet sln Pdmt.slnx add Pdmt.Api.Unit.Tests
   dotnet add Pdmt.Api.Unit.Tests reference Pdmt.Api
   ```
4. Перенести `InMemoryRateLimitServiceTests.cs` из Integration → Unit
5. Убедиться: `dotnet test` по обоим проектам проходит без изменений

### Шаг 2 — Unit-тесты контроллеров (2 дня)

Создать по файлу на контроллер в `Unit/Controllers/`:

| Файл | Приоритет |
|---|---|
| `EventsControllerTests.cs` | 🔴 Высокий |
| `AuthControllerTests.cs` | 🔴 Высокий |
| `TagsControllerTests.cs` | 🔴 Высокий |
| `InsightsControllerTests.cs` | 🟡 Средний |
| `AnalyticsControllerTests.cs` | 🟡 Средний |

Шаблон: ~15–20 тестов на контроллер (все HTTP-коды + edge cases параметров).

### Шаг 3 — Unit-тесты Middleware (0.5 дня)

1. `Unit/Middleware/ExceptionHandlingMiddlewareTests.cs` — 9 тестов
2. `Unit/Middleware/CorrelationIdMiddlewareTests.cs` — 3 теста

### Шаг 4 — Рефакторинг вычислений InsightsService (2 дня)

1. Вынести `Compute*` методы как `internal static`
2. Добавить `[assembly: InternalsVisibleTo("Pdmt.Api.Unit.Tests")]` в `Pdmt.Api`
3. Создать `Unit/Services/InsightsComputationTests.cs`
4. ~35–40 тестов для всех Compute-методов

### Шаг 5 — Дополнить rate limiting (0.5 дня)

1. Тест: сброс окна по истечении времени — внедрить `TimeProvider` (или
   `ISystemClock`) в `InMemoryRateLimitService` вместо `DateTime.UtcNow`
2. Тест: параллельные запросы к Redis rate limiter (мок IDatabase)

### Шаг 6 — Закрыть Integration-пробелы (1 день)

1. Реализовать `Integration/Controllers/AnalyticsControllerTests.cs` (сейчас пустой)
2. Создать `Integration/Controllers/WebAuthControllerTests.cs` (cookie-flow)
3. Дополнить граничные случаи в `EventServiceTests.cs` (intensity 0, 11)

### Шаг 7 — CI/CD покрытие (0.5 дня)

1. Добавить в pipeline: `dotnet test --collect:"XPlat Code Coverage"`
2. Добавить шаг ReportGenerator для HTML-отчёта как artifact
3. Настроить порог: build fails при coverage < 80%

---

## 8. Примеры тестов

### Пример 1: Unit-тест контроллера (Moq + FluentAssertions)

```csharp
// Pdmt.Api.Unit.Tests/Controllers/EventsControllerTests.cs
public class EventsControllerTests
{
    private readonly Mock<IEventService> _eventService = new();
    private readonly EventsController _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public EventsControllerTests()
    {
        _sut = new EventsController(_eventService.Object);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, _userId.ToString())]))
            }
        };
    }

    [Fact]
    public async Task GetEvent_EventNotFound_Returns404()
    {
        _eventService
            .Setup(s => s.GetByIdAsync(_userId, It.IsAny<Guid>()))
            .ReturnsAsync((EventResponseDto?)null);

        var result = await _sut.GetEvent(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateEvent_ValidDto_Returns201WithLocation()
    {
        var dto = new CreateEventDto { Title = "Test", Type = DtoEventType.Positive, Intensity = 5 };
        var created = new EventResponseDto { Id = Guid.NewGuid(), Title = "Test" };
        _eventService.Setup(s => s.CreateEventAsync(_userId, dto)).ReturnsAsync(created);

        var result = await _sut.CreateEvent(dto);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task UpdateEvent_ServiceReturnsFalse_Returns404()
    {
        _eventService
            .Setup(s => s.UpdateEventAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UpdateEventDto>()))
            .ReturnsAsync(false);

        var result = await _sut.UpdateEvent(Guid.NewGuid(), new UpdateEventDto());

        result.Should().BeOfType<NotFoundResult>();
    }
}
```

### Пример 2: Unit-тест Middleware

```csharp
// Pdmt.Api.Unit.Tests/Middleware/ExceptionHandlingMiddlewareTests.cs
public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int statusCode, JsonElement body)> InvokeWith(
        Exception ex, string environment = "Production")
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        var env = Mock.Of<IWebHostEnvironment>(e => e.EnvironmentName == environment);
        var logger = Mock.Of<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw ex, logger, env);

        await middleware.InvokeAsync(ctx);

        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = JsonSerializer.Deserialize<JsonElement>(
            await new StreamReader(ctx.Response.Body).ReadToEndAsync());

        return (ctx.Response.StatusCode, body);
    }

    [Theory]
    [InlineData(typeof(NotFoundException), 404)]
    [InlineData(typeof(UnauthorizedAccessException), 401)]
    [InlineData(typeof(InvalidOperationException), 400)]
    [InlineData(typeof(RateLimitExceededException), 429)]
    public async Task InvokeAsync_KnownException_ReturnsMappedStatus(Type exType, int expectedStatus)
    {
        var ex = (Exception)Activator.CreateInstance(exType, "test message")!;

        var (statusCode, _) = await InvokeWith(ex);

        statusCode.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500()
    {
        var (statusCode, _) = await InvokeWith(new Exception("boom"));

        statusCode.Should().Be(500);
    }

    [Fact]
    public async Task InvokeAsync_InProduction_HidesDetails()
    {
        var (_, body) = await InvokeWith(new Exception("boom"), "Production");

        body.GetProperty("details").GetString().Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_ExposesDetails()
    {
        var (_, body) = await InvokeWith(new Exception("boom"), "Development");

        body.GetProperty("details").GetString().Should().NotBeNullOrEmpty();
    }
}
```

### Пример 3: Unit-тест вычислительной логики Insights

```csharp
// Pdmt.Api.Unit.Tests/Services/InsightsComputationTests.cs
public class InsightsComputationTests
{
    [Fact]
    public void ComputeRepeatingTriggers_TagBelowMinCount_IsExcluded()
    {
        var tag = new Tag { Id = Guid.NewGuid(), Name = "stress" };
        var events = Enumerable.Range(0, 2).Select(_ => new Event
        {
            Type = EventType.Negative,
            Intensity = 7,
            EventTags = [new EventTag { Tag = tag }]
        }).ToList();

        var result = InsightsService.ComputeRepeatingTriggers(events, minCount: 3);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeRepeatingTriggers_PositiveEvents_AreIgnored()
    {
        var tag = new Tag { Id = Guid.NewGuid(), Name = "joy" };
        var events = Enumerable.Range(0, 5).Select(_ => new Event
        {
            Type = EventType.Positive,
            Intensity = 8,
            EventTags = [new EventTag { Tag = tag }]
        }).ToList();

        var result = InsightsService.ComputeRepeatingTriggers(events, minCount: 3);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeInfluenceabilitySplit_NoNegativeEvents_ReturnsZeros()
    {
        var events = new List<Event>
        {
            new() { Type = EventType.Positive, CanInfluence = true, Intensity = 8 }
        };

        var result = InsightsService.ComputeInfluenceabilitySplit(events);

        result.CanInfluenceAvg.Should().Be(0);
        result.CannotInfluenceAvg.Should().Be(0);
    }

    [Fact]
    public void ComputeWeekdayStats_NoEvents_ReturnsSevenZeroedDays()
    {
        var result = InsightsService.ComputeWeekdayStats([]);

        result.Should().HaveCount(7);
        result.Should().AllSatisfy(d => d.AvgIntensity.Should().Be(0));
    }
}
```

### Пример 4: Unit-тест AuthService (rate limit — граница)

```csharp
// Pdmt.Api.Unit.Tests/Services/AuthServiceUnitTests.cs
public class AuthServiceUnitTests
{
    private readonly Mock<IRateLimitService> _rateLimitMock = new();

    [Fact]
    public async Task RegisterAsync_RateLimitExceeded_ThrowsBeforeDbAccess()
    {
        _rateLimitMock
            .Setup(r => r.CheckAsync("Auth.Register", "127.0.0.1"))
            .ThrowsAsync(new RateLimitExceededException("Auth.Register"));

        var sut = CreateSut();

        Func<Task> act = () => sut.RegisterAsync(
            new UserDto("a@b.com", "password123"), "127.0.0.1");

        await act.Should().ThrowAsync<RateLimitExceededException>()
                 .WithMessage("*Auth.Register*");
    }

    private AuthService CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        return new AuthService(db, BuildTestConfig(), _rateLimitMock.Object);
    }

    private static IConfiguration BuildTestConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-min-32-chars-long!",
                ["Jwt:Issuer"] = "pdmt-test",
                ["Jwt:Audience"] = "pdmt-test",
                ["Jwt:TokenLifetimeMinutes"] = "60",
                ["Jwt:RefreshTokenLifetimeDays"] = "1",
            })
            .Build();
}
```

---

## 9. Антипаттерны — что убрать из текущих тестов

| Антипаттерн | Где встречается | Как исправить |
|---|---|---|
| InMemory EF в папке без пометки Integration | Все `*ServiceTests.cs` | Переместить в `Integration/` + добавить Trait |
| Длинный Arrange без хелперов | `EventServiceTests.cs` (830 строк) | Вынести `CreateEvent(...)`, `CreateUser(...)` в `TestHelpers` |
| Хардкод Guid-строк (`"00000000-0000-0000-0000-000000000001"`) | `InsightsControllerTests.cs` | `Guid.NewGuid()` + локальные переменные |
| Пустой класс-заглушка | `AnalyticsControllerTests.cs` | Реализовать или удалить файл |
| Нет тестов на деление на ноль | `InsightsService` методы с avg | Добавить тест с пустым списком |
| Тест проверяет только то, что не упало | Некоторые тесты без `result.Should()...` | Добавить конкретный assert на данные |

---

## 10. Итоговая матрица покрытия (цель)

| Компонент | Тип теста | Текущий статус | Приоритет |
|---|---|---|---|
| `ExceptionHandlingMiddleware` | Unit | ❌ Отсутствует | 🔴 Высокий |
| `CorrelationIdMiddleware` | Unit | ❌ Отсутствует | 🟡 Средний |
| `EventsController` | Unit | ❌ Отсутствует | 🔴 Высокий |
| `TagsController` | Unit | ❌ Отсутствует | 🔴 Высокий |
| `AuthController` | Unit | ❌ Отсутствует | 🔴 Высокий |
| `InsightsController` | Unit | ❌ Отсутствует | 🟡 Средний |
| `AnalyticsController` | Unit | ❌ Отсутствует | 🟡 Средний |
| InsightsService `Compute*` методы | Unit | ❌ Отсутствует | 🔴 Высокий |
| `InMemoryRateLimitService` | Unit | ✅ Покрыт | — |
| `AuthService` | Integration | ✅ Покрыт | — |
| `EventService` | Integration | ✅ Покрыт | — |
| `InsightsService` | Integration | ✅ Покрыт | — |
| `AnalyticsService` | Integration | ✅ Покрыт | — |
| `TagService` | Integration | ✅ Покрыт | — |
| `EventsController` (full flow) | Integration | ✅ Покрыт | — |
| `InsightsController` (full flow) | Integration | ✅ Покрыт | — |
| `AnalyticsController` (full flow) | Integration | ❌ Пустой файл | 🟡 Средний |
| `WebAuthController` (cookie flow) | Integration | ❌ Отсутствует | 🟡 Средний |
