# Pdmt.Api — Improvement Roadmap

> Глубокое ревью с фокусом на безопасность, надёжность auth-флоу, изоляцию слоёв, производительность и наблюдаемость.
> Контекст проекта: личное приложение (single-user / небольшой круг), деплой на render.com (планируется переезд).
> Каждый пункт: **приоритет** (P0/P1/P2), **размер** (S/M/L), **acceptance criteria**, ссылки на код. Ничего из «оверинжиниринга для масштаба» в P0/P1 не попало.

---

## Условные обозначения

- **P0** — security / корректность / источник наблюдаемых багов. Делать в первую очередь.
- **P1** — важно для стабильности и развития, но не «горит».
- **P2** — cleanup, читаемость, мелкие улучшения.
- **S** ≈ < 2 часов, **M** ≈ полдня, **L** ≈ 1+ день.
- Каждый пункт можно превратить в одно issue с готовыми Acceptance Criteria.

---

## 0. Корневые причины наблюдаемых auth-проблем

Ты упоминал, что вокруг refresh-токена «было много проблем» и подозреваешь, что часть из них — render.com. Разбираю по пунктам:

| Симптом | Причина | Только хостинг? | Где править |
|---|---|---|---|
| Случайные 401, после которых клиент logout-ится | **Race condition в refresh-флоу** + cold start усиливает | Нет, баг детерминированный | [Sec-3](#sec-3-refresh-token-race--reuse-detection) |
| Логин на одном устройстве «отрубает» другое | `LoginAsync` revoke-ает **все** активные refresh-токены | Нет | [Sec-2](#sec-2-loginasync-не-должен-ревокать-все-refresh-токены) |
| Rate-limit срабатывает «непонятно, для всех сразу» | `UseForwardedHeaders` не настроен под cloud-proxy → все клиенты с одним IP | Связано с хостингом, но фикс в коде | [Sec-1](#sec-1-настроить-useforwardedheaders-под-cloud-proxy) |
| Refresh cookie expires через 30 дней, конфиг говорит другое | Хардкод в `WebAuthController.SetRefreshCookie` (см. TODO.md) | Нет | [Arch-2](#arch-2-вынести-30-дней-из-webauthcontroller-todo) |

**Вывод:** переезд с render.com снизит частоту проявлений, но не уберёт корневые причины. Все четыре источника надо чинить в коде.

---

## 1. Security

### ✅ Sec-1. Настроить UseForwardedHeaders под cloud-proxy
- **Приоритет:** P0  **Размер:** S
- **Файл:** [Program.cs:181-184](Program.cs#L181)
- **Проблема:** `UseForwardedHeaders` подключён, но без `KnownProxies`/`KnownNetworks` middleware принимает `X-Forwarded-For` только от `127.0.0.1` и `::1` (defaults). На render.com / Fly.io / Railway прокси-сервер имеет другой IP, поэтому headers **молча игнорируются** и `RemoteIpAddress` всегда равен IP прокси. Следствие — rate-limit по IP работает per-instance, а не per-client (все логины делят один bucket из 5 попыток / 10 минут).
- **Acceptance:**
  - В `appsettings.{env}.json` добавить блок `ForwardedHeaders:KnownNetworks` (для известных подсетей) или включить опцию «доверять всем известным прокси» через `KnownProxies.Clear()` / `KnownNetworks.Clear()` — но **только** если за единственным управляемым L7-прокси.
  - Желательно вытащить решение в helper-метод и снабдить комментарием с обоснованием для каждой среды.
  - Проверить интеграционным тестом: `X-Forwarded-For: 1.2.3.4` → `HttpContext.Connection.RemoteIpAddress.ToString() == "1.2.3.4"`.
  - В логе на старте писать "Trusting forwarded headers from networks: …" (sanity check).

### ✅ Sec-2. LoginAsync не должен ревокать все refresh-токены
- **Приоритет:** P0  **Размер:** S
- **Файл:** [Services/AuthService.cs:71-72](Services/AuthService.cs#L71)
- **Проблема:** При логине ревокаются **все** активные refresh-токены пользователя. UX-сломано (логин на телефоне выкидывает с веба), и это активный источник флапов: если фоновый тред мобилки делает запрос ровно в момент логина с другого устройства — получает 401.
- **Acceptance:**
  - Login создаёт новый refresh-токен, старые **не трогает**.
  - (опционально) лимит N активных refresh-токенов на пользователя (например, 5) — при превышении ревокать самый старый.
  - Logout остаётся как есть (revoke all → см. Sec-7).
  - Тест: два последовательных логина → оба refresh-токена работают.

### ✅ Sec-3. Refresh token race + reuse detection
- **Приоритет:** P0  **Размер:** M
- **Файл:** [Services/AuthService.cs:88-103](Services/AuthService.cs#L88)
- **Проблема (двойная):**
  1. Если два запроса `/refresh` приходят с одним токеном (типичный сценарий: cold-start render.com, клиент таймаутит, ретраит) — один ротирует, второй видит токен revoked → 401 → клиент logout. **Детерминированный баг.**
  2. Нет detection-а replay-атаки: если refresh-токен украден и ротирован атакующим, легитимный клиент тоже попадёт на ротированный токен — но это не отличить от обычной ошибки. RFC 6819 §5.2.2.3 рекомендует invalidate всю «семью» токенов.
- **Acceptance:**
  - **Минимум:** в `RefreshAsync` запускать всё в `IDbContextTransaction` или через `RowVersion`/optimistic concurrency на `RefreshToken.IsRevoked`, плюс grace-window: токен считается валидным ещё ~30 секунд после ротации (поле `ReplacedAt` / `RotatedAt`). Параллельный второй запрос вернёт **тот же** новый токен (idempotent).
  - **Желательно:** ввести `RefreshToken.FamilyId` (Guid). При попытке использовать revoked-токен **вне grace window** — ревокать всю семью + лог security event. Это и убирает race, и даёт reuse-detection.
  - Тест: 5 параллельных `/refresh` с одним токеном → все возвращают валидный access token (один и тот же или эквивалентный), 0 × 401.
  - Тест: использовать revoked токен через 5 минут → 401 + все токены в семье revoked.

### ✅ Sec-4. Скрыть ex.Message в 500-ках в проде
- **Приоритет:** P0  **Размер:** S
- **Файл:** [Middleware/ExceptionHandlingMiddleware.cs:33-37,52-58](Middleware/ExceptionHandlingMiddleware.cs#L33)
- **Проблема:** для generic `Exception` `hideDetails: true`, но `ex.Message` всё равно записывается в `response.Message`. Сообщения системных исключений могут раскрывать структуру (`Connection refused for postgresql://...`, имена таблиц, путь файла).
- **Acceptance:**
  - Для catch-all 500 в проде: `Message = "Internal server error"`, `Details = null`. В dev можно оставить как есть.
  - `correlationId` — единственное, что отдаём, чтобы пользователь мог сослаться при поддержке.

### ✅ Sec-5. CSRF-защита на /api/auth/web/refresh
- **Приоритет:** P1  **Размер:** S
- **Файл:** [Controllers/WebAuthController.cs:43-55](Controllers/WebAuthController.cs#L43)
- **Проблема:** refresh-cookie с `SameSite=None`, и endpoint вызывается без явной anti-CSRF меры. CORS+`AllowCredentials` блокирует чтение **ответа** атакующим, поэтому украсть access-token нельзя. Но атакующий может тригерить ротацию refresh-токена с произвольной страницы → DoS-вектор: легитимный клиент после этого видит 401 на каждом следующем запросе.
- **Acceptance:**
  - Минимум: проверка `Origin` header против allow-list (тот же, что в CORS-policy). Если не совпадает — 403.
  - Альтернатива: double-submit cookie или custom header `X-Csrf-Token`, но это усложнение, для single-user избыточно.
  - Опасения отметить в `intentional-non-goals` если решено остановиться на проверке Origin.

### ✅ Sec-6. Hide internal exception types в маппинге
- **Приоритет:** P1  **Размер:** S
- **Файл:** [Middleware/ExceptionHandlingMiddleware.cs:29-32](Middleware/ExceptionHandlingMiddleware.cs#L29)
- **Проблема:** `InvalidOperationException → 400` опасно: EF Core, Npgsql, BCL спокойно бросают `InvalidOperationException` по разным причинам, не связанным с бизнес-валидацией. Например, `BCrypt.Verify` с битым hash → `InvalidOperationException` → 400 «битый хеш» вместо 500.
- **Acceptance:**
  - Ввести `Pdmt.Api.Infrastructure.Exceptions.ValidationException` (или `BadRequestException`) и бросать только её для 400.
  - `InvalidOperationException` → попадает в catch-all 500.
  - Прошерстить сервисы и заменить `throw new InvalidOperationException(...)` на новую.

### ✅ Sec-7. Logout — текущая сессия vs все устройства
- **Приоритет:** P1  **Размер:** S
- **Файл:** [Services/AuthService.cs:114-124](Services/AuthService.cs#L114), [Controllers/AuthController.cs:50](Controllers/AuthController.cs#L50)
- **Проблема:** `LogoutAsync(userId)` ревокает **все** refresh-токены пользователя. Семантика «logout с текущего устройства» отсутствует.
- **Acceptance:**
  - Контроллер передаёт refresh-токен (из cookie/body) в сервис; ревокается только этот токен.
  - Опционально — отдельный endpoint `/logout-all` для «выйти со всех устройств».
  - Идемпотентность: повторный logout не падает.

### ✅ Sec-8. Email — ToLowerInvariant и нормализация
- **Приоритет:** P1  **Размер:** S
- **Файл:** [Services/AuthService.cs:21,53](Services/AuthService.cs#L21)
- **Проблема:** `.ToLower()` зависит от текущей культуры. Турецкая `İ` → `i̇` ломает уникальность email при определённых локалях контейнера. На уровне БД email хранится без CITEXT-типа, поэтому всё держится на нормализации в коде.
- **Acceptance:**
  - Заменить на `.ToLowerInvariant()` везде.
  - Добавить unit-тест с турецким `İ` (он же кейс с `Tag.Name`, кстати).

### ✅ Sec-9. HSTS в production
- **Приоритет:** P1  **Размер:** S
- **Файл:** [Program.cs:189-191](Program.cs#L189)
- **Проблема:** в проде нет ни `app.UseHsts()`, ни `app.UseHttpsRedirection()`. За reverse proxy с TLS-терминацией HTTPS-redirection не нужен, но HSTS-header клиенту лучше отдавать всегда (ответ всё равно идёт через TLS).
- **Acceptance:**
  - Подключить `app.UseHsts()` в проде.
  - max-age — стартовать с 30 дней, потом увеличить.
  - Документировать в CLAUDE.md, что reverse proxy должен пропускать `Strict-Transport-Security`.

### ✅ Sec-10. FailedLoginAttempts — использовать или удалить
- **Приоритет:** P1  **Размер:** M
- **Файл:** [Services/AuthService.cs:59-65](Services/AuthService.cs#L59), [Domain/FailedLoginAttempt.cs](Domain/FailedLoginAttempt.cs)
- **Проблема:** таблица растёт, но не используется. Либо реализуем lockout, либо убираем сущность.
- **Acceptance (если оставлять):**
  - В `LoginAsync` после `BCrypt.Verify` неуспеха проверять количество failed attempts по `email` за последние N минут. При превышении — `UnauthorizedAccessException("Account temporarily locked")` (но **не** делать это per-IP, иначе атакующий с одного IP убивает чужой аккаунт).
  - Cleanup-задача: удалять записи старше 30 дней.
  - Расширить `OccurredAtUtc` индекс до `(Email, OccurredAtUtc)` для быстрых выборок.
- **Acceptance (если убирать):**
  - Удалить сущность, миграция, поле в `AppDbContext`, использования. Обосновать в CLAUDE.md.

### Sec-11. Валидация Jwt:Secret на минимальную длину
- **Приоритет:** P2  **Размер:** S
- **Файл:** [Program.cs:28-33](Program.cs#L28)
- **Проблема:** проверяется только `IsNullOrWhiteSpace`. HS256 ключ < 32 байт — security weakness (HMAC-SHA256 рекомендует ≥ 256 бит = 32 байта raw).
- **Acceptance:** при `jwtSecret.Length < 32` (символов в UTF-8 ≈ байт) → throw с явным сообщением.

### Sec-12. JWT — добавить jti
- **Приоритет:** P2  **Размер:** S
- **Файл:** [Services/AuthService.cs:131-135](Services/AuthService.cs#L131)
- **Проблема:** access-token не содержит `jti`. Если когда-нибудь понадобится server-side denylist или замена скомпрометированного токена раньше его 60-минутного TTL — нечего инвалидировать индивидуально.
- **Acceptance:** `new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())`. Для текущего масштаба denylist не делаем — это просто задел на будущее.

---

## 2. Architecture / Layering

### ✅ Arch-1. Разделить service-result и transport-DTO для auth
- **Приоритет:** P0 (это и есть нарушение изоляции, которое ты подозревал)  **Размер:** M
- **Файлы:** [Services/IAuthService.cs](Services/IAuthService.cs), [Services/AuthService.cs](Services/AuthService.cs), [Controllers/WebAuthController.cs:22-28,38-40](Controllers/WebAuthController.cs#L22), [Dto/AuthResultDto.cs](Dto/AuthResultDto.cs), [Dto/WebAuthResultDto.cs](Dto/WebAuthResultDto.cs)
- **Проблема:** `IAuthService` возвращает `AuthResultDto` — DTO с `RefreshToken` в теле. `WebAuthController` достаёт оттуда `RefreshToken`, кладёт в cookie и **руками** конструирует `WebAuthResultDto`. Это работает только потому, что Web-контроллер дисциплинирован. Случайная правка (например, `[ProducesResponseType(typeof(AuthResultDto), …)]` в Web-action или маппер на webhook) — и refresh-токен утечёт в JSON web-клиента, обходя cookie-only контракт. Сервис знает про два разных транспортных DTO больше, чем нужно.
- **Acceptance:**
  - Ввести в `Services/` транспорт-нейтральный record `AuthResult(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt)`.
  - `IAuthService` возвращает `AuthResult`.
  - `AuthController` мапит `AuthResult → AuthResultDto` (full).
  - `WebAuthController` мапит `AuthResult → WebAuthResultDto` (без refresh-токена) + `SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt)`.
  - DTO-классы остаются в `Dto/`, internal model — в `Services/`.
  - Тест: `AuthResultDto` не используется в `WebAuthController` ни в каком виде (architecture test или просто grep).

### ✅ Arch-2. Вынести 30 дней из WebAuthController (TODO)
- **Приоритет:** P1  **Размер:** S (после Arch-1 — тривиально)
- **Файл:** [Controllers/WebAuthController.cs:74](Controllers/WebAuthController.cs#L74)
- **Проблема:** `Expires = DateTimeOffset.UtcNow.AddDays(30)` хардкод. Если поменяется `Jwt:RefreshTokenLifetimeDays` (сейчас тоже 30, но всё впечатление совпадения) — cookie-expiry рассинхронизируется с серверной валидностью. Уже отмечено в `TODO.md`.
- **Acceptance:** `SetRefreshCookie` принимает `DateTimeOffset expiresAt`, который приходит из `AuthResult` (см. Arch-1). Никаких хардкодов.

### ✅ Arch-3. Унификация валидации диапазона дат
- **Приоритет:** P1  **Размер:** S
- **Файлы:** [Controllers/AnalyticsController.cs:32-33](Controllers/AnalyticsController.cs#L32), [Services/InsightsService.cs:15-19](Services/InsightsService.cs#L15)
- **Проблема:** `from > to` проверяется в `AnalyticsController.GetCorrelations` (контроллер), но в `InsightsService.ValidateDateRange` (сервис). CLAUDE.md явно говорит «No business logic in controllers». В `AnalyticsController.GetWeeklySummary` / `GetCalendarWeek` валидации вообще нет.
- **Acceptance:**
  - Перенести валидацию в `AnalyticsService` (как уже сделано в `InsightsService`).
  - Поднять exception type до `ValidationException` (см. Sec-6).
  - Удалить `if (from > to)` из контроллера.

### ✅ Arch-4. Убрать двойную загрузку в Update/Delete event
- **Приоритет:** P2  **Размер:** S
- **Файл:** [Controllers/EventsController.cs:65-71,77-83](Controllers/EventsController.cs#L65)
- **Проблема:** контроллер делает `GetByIdAsync` (1 SELECT), потом сервис ещё раз грузит entity внутри Update/Delete. Два round-trip на одну операцию.
- **Acceptance:**
  - `UpdateEventAsync` уже возвращает `bool` — использовать его: `if (!await service.UpdateEventAsync(...)) return NotFound();`.
  - `DeleteEventAsync` сделать возвращающей `bool` так же.
  - Убрать предварительные `GetByIdAsync` из контроллера.

### ✅ Arch-5. Убрать защитные `userId == Guid.Empty` в сервисах
- **Приоритет:** P2  **Размер:** S
- **Файлы:** [Services/EventService.cs:19,49,65,94](Services/EventService.cs#L19), [Services/AuthService.cs](Services/AuthService.cs)
- **Проблема:** все вызовы идут через `[Authorize]` контроллеры; `User.GetUserId()` уже бросает `InvalidOperationException` при отсутствии claim. Дополнительные проверки — мёртвый код, дают ложное чувство безопасности.
- **Acceptance:** убрать проверки. Опираться на гарантии аутентификационного pipeline + extension method.

### ✅ Arch-6. EventService — лишняя `if (ev is null)` проверка
- **Приоритет:** P2  **Размер:** S
- **Файл:** [Services/EventService.cs:63-64](Services/EventService.cs#L63)
- **Проблема:** `[ApiController]` сам отвергает null-body (400). CLAUDE.md прямо говорит «No manual `if (model == null)`».
- **Acceptance:** убрать.

### ✅ Arch-7. Удалить пустой `JwtGenerator.cs`
- **Приоритет:** P2  **Размер:** S
- **Файл:** [Infrastructure/JwtGenerator.cs](Infrastructure/JwtGenerator.cs) — пустой класс.
- **Acceptance:** удалить файл.

### ✅ Arch-8. Использовать IOptions для Jwt-конфига
- **Приоритет:** P2  **Размер:** S
- **Файл:** [Services/AuthService.cs:128-129,147](Services/AuthService.cs#L128)
- **Проблема:** `int.Parse(jwt["TokenLifetimeMinutes"]!)` парсится при каждом login/register/refresh. Плюс null-forgiving `!` — при опечатке в конфиге runtime-крэш в горячем пути, а не на старте.
- **Acceptance:**
  - `JwtOptions { Issuer, Audience, TokenLifetimeMinutes, RefreshTokenLifetimeDays }`.
  - `builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"))`.
  - В `AuthService` инжектится `IOptions<JwtOptions>`.
  - Валидация на старте через `ValidateDataAnnotations()`/`ValidateOnStart()`.

---

## 3. Reliability / Concurrency

### ✅ Rel-1. CancellationToken через всю цепочку
- **Приоритет:** P1  **Размер:** M
- **Файлы:** все Controllers + Services
- **Проблема:** `CancellationToken` не пробрасывается. Если клиент закрыл вкладку или таймаутил, сервер всё равно дочитывает запрос (особенно тяжёлые insights). Под cold-start это особенно дорого.
- **Acceptance:**
  - Action method принимает `CancellationToken ct` (ASP.NET сам мапит на `HttpContext.RequestAborted`).
  - Service-метод принимает `CancellationToken`.
  - Все `ToListAsync()` / `FirstOrDefaultAsync()` / `SaveChangesAsync()` получают токен.
  - Тест: при отмене запроса операция прерывается (можно через `HttpClient.Timeout` + проверка логов).

### ✅ Rel-2. Migration retry на старте
- **Приоритет:** P1  **Размер:** S
- **Файл:** [Program.cs:165-172](Program.cs#L165)
- **Проблема:** `db.Database.MigrateAsync()` бросит и упадёт контейнер, если БД не готова в момент старта (типичная ситуация после redeploy / managed-Postgres provisioning lag). На render.com приводит к failed-deploy и автоматическому reverter-у.
- **Acceptance:**
  - Polly или ручной retry: 5 попыток с экспоненциальным backoff 1→2→4→8→16 секунд.
  - На каждой неудаче — log warning с попыткой и причиной.
  - Если все попытки исчерпаны — крэш с понятным сообщением.

### ✅ Rel-3. Health check — разделить liveness и readiness
- **Приоритет:** P1  **Размер:** S
- **Файл:** [Program.cs:122-124,195](Program.cs#L122)
- **Проблема:** один `/health` с `AddNpgSql + AddRedis`. Если Postgres временно лежит, hosting-платформа решит «контейнер мёртв» и перезапустит — тогда как процесс жив и при возврате Postgres вернётся в строй сам.
- **Acceptance:**
  - `/health/live` — без зависимостей, всегда 200 если процесс работает (filter: `Predicate = _ => false`).
  - `/health/ready` — Postgres + Redis (текущее поведение).
  - На hosting (next provider) liveness привязать к `/health/live`, readiness — к `/health/ready`.
  - Документировать в CLAUDE.md.

### ✅ Rel-4. Подключить TokenCleanupBgService и почистить
- **Приоритет:** P2  **Размер:** S
- **Файл:** [Services/TokenCleanupBgService.cs](Services/TokenCleanupBgService.cs), [Program.cs:158](Program.cs#L158)
- **Проблема:** сервис закомментирован. Без него `RefreshTokens` растёт (revoked+expired никогда не удаляются). Также unused variable `expired` (warning).
- **Acceptance:**
  - Раскомментировать регистрацию.
  - Убрать unused `expired` или логировать его (`logger.LogInformation("Cleaned {Count} expired refresh tokens", expired)`).
  - Обработка `OperationCanceledException` от `Task.Delay`.
  - Try/catch в цикле — иначе один сбой в БД убивает весь сервис на весь uptime контейнера.

### Rel-5. ResolveTagsAsync race condition
- **Приоритет:** P2  **Размер:** M
- **Файл:** [Services/EventService.cs:135-166](Services/EventService.cs#L135)
- **Проблема:** два параллельных `CreateEventAsync` с одним новым `tagName` → оба создают `Tag` → второй `SaveChanges` падает на unique constraint `(UserId, Name)`. Для single-user маловероятно, но возможно (мобильное приложение + быстрые клики).
- **Acceptance:**
  - Catch `DbUpdateException` от unique violation → reload тегов и повторить attach.
  - Альтернатива: PostgreSQL `INSERT ... ON CONFLICT DO NOTHING RETURNING` — но это требует raw SQL, что против CLAUDE.md.
  - Предложение: оставить как low-priority known issue, пометить «не делаем для single-user» если решено отложить.

### Rel-6. Insights — boundary бага с локальной timezone
- **Приоритет:** P2  **Размер:** M
- **Файлы:** [Services/InsightsService.cs](Services/InsightsService.cs), все методы используют `e.Timestamp >= from && e.Timestamp < to.AddDays(1)`
- **Проблема:** `from`/`to` приходят из контроллера как UTC `DateTimeOffset`. `to.AddDays(1)` — это UTC+1 день. Но локальные дни (Europe/Vilnius, UTC+2/+3) сдвинуты — событие `2026-05-05 23:30 local` (= `2026-05-05 21:30 UTC`) при запросе `to=2026-05-05` попадает в выборку, а событие `2026-05-06 01:30 local` (= `2026-05-05 23:30 UTC`) — тоже попадает, хотя пользователь его в «дне 5-го» не ожидает.
- В `AnalyticsService.GetCalendarMonth` это сделано **правильно**: `TimeZoneInfo.ConvertTimeToUtc(local, tz)` для границ. В `InsightsService` — нет.
- **Acceptance:**
  - Унифицировать: внутри сервиса `from`/`to` (DateOnly или local-day-aligned DateTimeOffset) → `[fromUtc, toUtc)` через TimeZoneInfo.
  - Удалить `to.AddDays(1)` хак.
  - Тест: событие в 23:00 UTC последнего дня периода входит/не входит правильно.

---

## 4. Performance / DB

### ✅ Perf-1. Pagination в EventService.GetEvents
- **Приоритет:** P1  **Размер:** M
- **Файл:** [Services/EventService.cs:10-45](Services/EventService.cs#L10), [Controllers/EventsController.cs:17-40](Controllers/EventsController.cs#L17)
- **Проблема:** возвращает все события за период без лимита. Через год активного использования — несколько тысяч записей, каждая с тегами. Под cold-start это лишние секунды плюс память.
- **Acceptance:**
  - `[FromQuery] int? page = 1, [FromQuery] int? pageSize = 100` (clamp pageSize ≤ 500).
  - Возврат: `{ items: [...], total: N, page, pageSize }` (или просто items + `X-Total-Count` header).
  - На фронте — обновить список с пагинацией / infinite-scroll.
  - Default-сортировка `Timestamp DESC` (сейчас её нет — отдаётся в произвольном порядке).

### Perf-2. Лишние индексы на Events
- **Приоритет:** P2  **Размер:** S
- **Файл:** [Data/AppDbContext.cs:36-41](Data/AppDbContext.cs#L36)
- **Проблема:** одновременно есть `(UserId)`, `(Timestamp)`, `(UserId, Timestamp)`. Композитный индекс `(UserId, Timestamp)` уже покрывает запросы по `UserId` (left-prefix). Single-column `(UserId)` избыточен.
- Также упомянуто в CLAUDE.md: «AppDbContext has composite indexes on `Events(UserId, Timestamp)` and `Events(UserId, Type)`» — но `(UserId, Type)` отсутствует, упоминание stale. Либо добавить, либо обновить CLAUDE.md.
- **Acceptance:**
  - Удалить index `IX_Events_UserId` (миграция).
  - `IX_Events_Timestamp` — оставить если есть глобальные cross-user аналитики (сейчас нет — можно тоже удалить).
  - Обновить CLAUDE.md соответственно.

### Perf-3. Insights — async-проекция на стороне БД
- **Приоритет:** P2  **Размер:** L (в один заход — нет смысла)
- **Файлы:** [Services/InsightsService.cs](Services/InsightsService.cs)
- **Проблема:** все методы вытягивают ВСЕ события за период с `.Include(et).ThenInclude(t)` и считают в памяти. На периоде > 6 месяцев тяжело — особенно `GetTagCombosAsync` (двойная итерация по дням × парам тегов).
- **Acceptance / план:**
  - **Сейчас не делать.** Для single-user объёмы малы, преждевременная оптимизация.
  - Поставить watchdog: log если `events.Count > 5000` в любом insight — будет триггер на оптимизацию.
  - Для `tag-combos` — рассмотреть materialized view, но **не сейчас** (см. intentional-non-goals).

### Perf-4. ConnectionMultiplexer.Connect — async pattern
- **Приоритет:** P2  **Размер:** S
- **Файл:** [Program.cs:116-121](Program.cs#L116)
- **Проблема:** `ConnectionMultiplexer.Connect()` — синхронный, может зависнуть на старте если Redis недоступен. С `AbortOnConnectFail = false` он не падает, а просто пытается reconnect — но первый запрос к Redis блокируется.
- **Acceptance:**
  - Заменить на `ConnectAsync` через `Lazy<Task<IConnectionMultiplexer>>` — но это усложняет DI. Альтернатива — оставить с retry-policy через Polly.
  - Минимум: подсветить в логе на старте, если Redis недоступен.

---

## 5. Observability

### ✅ Obs-1. Логировать non-500 exceptions на Warning
- **Приоритет:** P1  **Размер:** S
- **Файл:** [Middleware/ExceptionHandlingMiddleware.cs:17-38](Middleware/ExceptionHandlingMiddleware.cs#L17)
- **Проблема:** только generic 500 логируются. 401/404/400/429 — невидимы в логах/трейсах. Для security audit (брут-форс, перебор event-id) нужно видеть всплески.
- **Acceptance:**
  - Все catch-блоки делают `logger.LogWarning(ex, "Handled {ExceptionType}: {Message}", typeof(T).Name, ex.Message)` — Warning level.
  - Generic — Error.
  - Rate-limit нарушения — особенно важны: лог + увеличить metric (см. Obs-2).

### ✅ Obs-2. Аудит auth-событий + метрики rate-limit
- **Приоритет:** P1  **Размер:** M
- **Файлы:** [Services/AuthService.cs](Services/AuthService.cs), [Services/CompositeRateLimitService.cs](Services/CompositeRateLimitService.cs)
- **Проблема:** ни logging, ни metrics для login-success / login-fail / refresh-success / refresh-fail / rate-limit-trip. В Seq невозможно понять, был ли всплеск ошибок или один пользователь систематически фейлится.
- **Acceptance:**
  - Structured logs: `logger.LogInformation("auth.login.success {UserId} {Ip}", ...)` etc.
  - OTel-counter: `auth.login.attempts` (labels: outcome=success/fail), `auth.refresh.attempts`, `rate_limit.tripped` (label: rule).
  - Дашборд (или sample-query в Seq) для security audit.

### ✅ Obs-3. HttpLoggingMiddleware vs OTel AspNetCore Instrumentation
- **Приоритет:** P2  **Размер:** S
- **Файл:** [Middleware/HttpLoggingMiddleware.cs](Middleware/HttpLoggingMiddleware.cs), [Program.cs:138-139](Program.cs#L138)
- **Проблема:** `AddAspNetCoreInstrumentation()` уже логирует HTTP request/duration через OTel. Кастомный middleware дублирует это в Seq. Пересечение — двойной шум.
- **Acceptance:**
  - Решить: либо OTel, либо middleware. Рекомендую OTel, а UserId tag добавлять через `Activity.Current?.SetTag` в auth-pipeline (например, в обработчике события OnTokenValidated).
  - Удалить `HttpLoggingMiddleware` если уйти на OTel-only.

### Obs-4. Fallback IP "unknown" — отдельный rate-limit bucket
- **Приоритет:** P2  **Размер:** S
- **Файлы:** [Controllers/AuthController.cs:19,30,41](Controllers/AuthController.cs#L19), [Controllers/WebAuthController.cs:23,37,49](Controllers/WebAuthController.cs#L23)
- **Проблема:** при недоступном `RemoteIpAddress` (например, Unix-socket, тестовая среда) все клиенты складываются в bucket `"unknown"`. Лимит 5/10мин на всех — тривиально завалить легитимный логин одной попыткой.
- **Acceptance (после Sec-1):**
  - Если IP undefined — return 503 «Service misconfigured» или просто log warning и пропустить rate-limit (тогда Sec-1 становится критичнее).
  - Лучше: IP — required для auth-actions, fallback не допускается.

---

## 6. Configuration

### Cfg-1. Документировать переезд с render.com
- **Приоритет:** P1  **Размер:** S
- **Файл:** новый раздел в [README.md](../README.md) или CLAUDE.md
- **Контекст:** триал Postgres на render закончился, нужен новый хостинг. Кандидаты:
  - **Fly.io** — есть free Postgres tier (1 ГБ), хороший cold-start, дёшево.
  - **Railway** — $5/мес кредит, простой deploy.
  - **Neon** (managed Postgres only) + любой хостинг для API (Fly.io/Render).
  - **Supabase** Postgres (free 500 МБ) + Render для API.
  - **Aiven** / **ElephantSQL** (после shutdown в 2024) — пропускаем.
  - **Self-host на VPS** (Hetzner CX11 ~€4/мес) — больше контроля, больше работы.
- **Acceptance (организационное):**
  - Решить хостинг для Postgres (рекомендую Neon — managed, free tier щедрее, не зависит от хостинга API).
  - Решить хостинг для API (Fly.io не засыпает в дешёвом плане; Render free засыпает).
  - Перенести Redis (Upstash free tier — 10к команд/день).
  - Обновить `.env.example` — добавить пример для нового провайдера.
  - Документировать в README.md процедуру миграции данных (pg_dump → pg_restore).

### Cfg-2. Не комитить пустой пароль в Development
- **Приоритет:** P2  **Размер:** S
- **Файл:** [appsettings.Development.json:9](appsettings.Development.json#L9)
- **Проблема:** `Password=` (пустой) — закомичен. Хрупко: если у разраба Postgres с паролем, надо переписать вручную.
- **Acceptance:**
  - Использовать user-secrets для пароля dev-БД (как уже сделано для Jwt:Secret).
  - В `appsettings.Development.json` оставить только не-секретные поля.
  - Обновить CLAUDE.md (раздел Build & Run).

---

## 7. Intentional non-goals (намеренно НЕ делаем)

Документирую решения «делать как есть», чтобы они не всплывали повторно:

- **2FA / TOTP / WebAuthn** — single-user app, threat model не требует. Сложность не оправдана.
- **RS256 / асимметричные ключи** — один сервер валидирует и подписывает; HS256 + 32+ байт секрет покрывает.
- **API versioning (`/api/v1/...`)** — клиенты под контролем (свои MAUI + React + Blazor), breaking changes координируются вручную.
- **Per-user timezone** — пока пользователь один. CLAUDE.md уже отмечает «replace config lookup with user.TimeZone when needed».
- **Полная RBAC / multi-tenant isolation на уровне DB (RLS)** — единый владелец данных, фильтрация по `UserId` в сервисах достаточна.
- **Account lockout по неудачам без cooldown** — DoS-вектор. Решение: либо короткое окно (15 мин), либо вообще не делать (Sec-10 со знаком minus).
- **Password complexity beyond MinLength=8 / breach-corpus check (HaveIBeenPwned API)** — single-user, owner и есть юзер.
- **Materialized views / aggregation tables для insights** — текущий объём (тысячи событий на пользователя) считается in-memory за миллисекунды. Делать только когда watchdog (см. Perf-3) сработает.
- **CSP / другие security headers через middleware** — API не отдаёт HTML, headers не нужны (кроме HSTS — см. Sec-9).
- **Refresh-токен в Authorization Code Flow / OAuth2 spec** — слишком тяжело для self-issued auth.
- **Audit log в отдельную таблицу** — Seq + structured logging (см. Obs-2) покрывает текущие нужды.
- **Distributed tracing across services** — один сервис, не нужно. OTel-конфиг готов на случай расширения.

---

## 8. Suggested issue rollout order

Ниже — рекомендуемый порядок превращения пунктов в issues с учётом зависимостей и затрагиваемого риска:

1. **Sprint 1 (Security & Auth flow):** Sec-1 → Sec-2 → Sec-3 → Arch-1 → Arch-2. Параллельно — миграция хостинга (Cfg-1).
2. **Sprint 2 (Robustness):** Rel-1 → Rel-2 → Rel-3 → Sec-4 → Sec-6 → Sec-7.
3. **Sprint 3 (Hygiene & UX):** Sec-5 → Sec-8 → Sec-9 → Sec-10 → Arch-3 → Arch-4 → Arch-5/6/7/8.
4. **Sprint 4 (Operability):** Obs-1 → Obs-2 → Obs-3 → Perf-1 → Rel-4.
5. **Backlog:** Perf-2/3/4, Rel-5/6, Sec-11/12, Obs-4, Cfg-2.

Sprint 1 — это то, что снимает 90% наблюдаемых auth-проблем. Если впишешься только в него — уже большая победа.

---

## 9. Зависимости между пунктами (важно для планирования)

- **Sec-3 после Sec-2** (логин больше не ревокает чужие токены — race с logout-all уходит).
- **Arch-2 после Arch-1** (refresh-cookie expiry берётся из service-result).
- **Sec-7 после Arch-1** (logout-эндпоинт принимает refresh-токен → нужен унифицированный путь его получения).
- **Obs-4 после Sec-1** (когда forwarded-headers исправлены, "unknown" IP становится действительно редким — можно сделать его жёстким fallback).
- **Cfg-1 после Sec-1** (на новом хостинге сразу настроить trusted proxies корректно).

---

## 10. Что я НЕ ревьюил (по договорённости)

- **Pdmt.Maui, Pdmt.Client, pdmt-web** — только в части контракта API.
- **Тесты** (`Pdmt.Api.Unit.Tests`, `Pdmt.Api.Integration.Tests`) — кроме структуры.
- **CI/CD pipeline, deploy-скрипты** — нет в репо для API.
- **TODO.md пункты, не относящиеся к API** — Web/MAUI задачи.

---

_Last updated: 2026-05-05_
