# TODO

- [ ] **MAUI: перевод на EN** — UI сейчас частично на русском
- [ ] **Web: перевод на EN** — UI сейчас частично на русском
- [ ] **InsightsController: вынести валидацию from > to из контроллера в сервис через InvalidOperationException
- [ ] **Web: useEventList: или убрать кастомный хук, или унифицировать по всему проекту
- [ ] **GitHubActions** - сконфигать CI
- [ ] **Нет loading skeleton / Suspense** — текст "Загрузка..." вместо skeleton-ов, layout shift при каждом переходе
- [ ] **Дата-логика без библиотеки** — ручные вычисления дат хрупки при DST-переходах и таймзонах
- [ ] **Testcontainers: Npgsql DateTimeOffset** — in-memory EF хранит offset как есть, Npgsql нормализует в UTC; нужны интеграционные тесты с реальным PostgreSQL для CreateEventAsync / UpdateEventAsync / GetByIdAsync с offset +03:00
- [ ] **InsightsService: расширить тесты** — покрыть все методы (GetTrendsAsync, GetTagTrendAsync, GetWeekdayStatsAsync, GetTagCombosAsync, GetNextDayEffectsAsync) с акцентом на группировку по дням/неделям на границах таймзоны Europe/Vilnius; использовать Testcontainers вместо in-memory
- [ ] **Seed data для новых пользователей** — наполнять аккаунт тестовыми событиями и тегами при регистрации, чтобы инсайты сразу показывали данные (онбординг)
