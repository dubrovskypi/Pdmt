# TODO — before Phase 4 / deploy

## Bugs

- [x] **pdmt-web: `EventListPage`** — `getTags()` вызывается при каждом изменении фильтра (в т.ч. tag filter). Вынести в отдельный `useEffect([], [])`, события грузить отдельно
- [x] **MAUI: Insights loading** — двойной спиннер при открытии страницы, возможный memory leak; загружать карточки 1–2 первыми, остальные в фоне
- [x] **MAUI: Card 5 (Discounted Positives)** — карточка пустая; проверить `GetDiscountedPositivesAsync` и порог `count >= 5 AND avgIntensity < 4.0`

## Phase 3 — незавершённое (pdmt-web)

- [ ] **`InsightsPage.tsx`** — карусель из 10 карточек (ROADMAP §3.4 Screen 5); lazy-load каждой карточки отдельно
- [ ] **`AnalyticsPage.tsx`** — weekly summary + trend chart через Recharts (ROADMAP §3.4 Screen 6)

## Рефакторинг

- [ ] **`InsightsController` + `InsightsService`** — вынести 6 insights-методов из `AnalyticsController`/`AnalyticsService`; маршрут остаётся `api/analytics/insights/*`; `GetMonday` в статический хелпер

## Deploy

- [ ] **CORS prod origins** — заполнить `Cors.AllowedOrigins` в `appsettings.Prod.json` после того как pdmt-web задеплоен на домен
- [ ] **MAUI: перевод на EN** — UI сейчас частично на русском (todo.txt п.3)

## Технический долг (не блокирует деплой)

- [x] **`TagResponseDto.EventCount`** — добавить `int EventCount` в DTO и `EventCount = t.EventTags.Count` в `TagService.GetTagsAsync`
- [x] **`ICollection<T>` вместо `List<T>`** — единым проходом по всем nav properties (`User`, `Event`, `Tag`, `EventTag`) когда трогаем энтити
- [x] **`DateTimeOffset` миграция (API)** — обновлены Controllers, Services, IEventService и IAnalyticsService; Domain entities и DTOs уже используют DateTimeOffset
- [x] **`DateTimeOffset` миграция (Clients – Blazor)** — обновить Blazor клиента
- [x] **`DateTimeOffset` миграция (Clients – MAUI)** — обновить MAUI клиента
- [x] **`DateTimeOffset` миграция (Clients – React)** — обновить React клиента
- [x] **EF Migration** — создана пустая миграция для Postgres (DateTimeOffset и DateTime оба маппируются в timestamp with time zone)
