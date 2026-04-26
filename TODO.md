# TODO

- [ ] **MAUI: перевод на EN** — UI сейчас частично на русском
- [ ] **Web: перевод на EN** — UI сейчас частично на русском
- [x] **API: InsightsController** - вынести валидацию from > to из контроллера в сервис через InvalidOperationException
- [ ] **Web: Hooks** - useEventList или убрать кастомный хук, или унифицировать по всему проекту
- [ ] **Web: Нет loading skeleton / Suspense** - текст "Загрузка..." вместо skeleton-ов, layout shift при каждом переходе
- [ ] **Web: Дата-логика без библиотеки** - ручные вычисления дат хрупки при DST-переходах и таймзонах
- [x] **Tests: Testcontainers-Npgsql-DateTimeOffset** - in-memory EF хранит offset как есть, Npgsql нормализует в UTC; нужны интеграционные тесты с реальным PostgreSQL для CreateEventAsync / UpdateEventAsync / GetByIdAsync с offset +03:00
- [x] **Tests: InsightsServiceTests** - расширить тесты: покрыть все методы с акцентом на группировку по дням/неделям на границах таймзоны Europe/Vilnius; использовать Testcontainers вместо in-memory
- [ ] **API: Seed data для новых пользователей** — наполнять аккаунт тестовыми событиями и тегами при регистрации, чтобы инсайты сразу показывали данные (онбординг)
- [x] **Tests: WebAuthController** unit tests
- [ ] **MAUI: диалоги** — рисовать диалог с подтверждением для удаления события, или выхода с редактирования или добавления нового события 
