# Pdmt.Maui — итеративный редизайн под design_handoff_pdmt_maui

## Context

В `Pdmt Design System/design_handoff_pdmt_maui/` лежит финальный дизайн Android UI (README + HTML‑прототип + JSX‑фрейм) на 6 экранов: Login, Events, New/Edit Event, Calendar (Week+Month), Insights, Account. Дизайн high‑fidelity: новые токены цветов и типографики (Roboto), иные радиусы, новый bottom‑sheet для Login, extended FAB на Events, Month Grid в Calendar (отсутствует в текущем коде), tappable dots в Insights.

Текущая `Pdmt.Maui` уже содержит структуру страниц 1:1, но визуально расходится: Primary `#1565C0` вместо teal `#006a60`, нет dark‑токенов, FAB круглый «+» вместо extended pill, в `WeeklyCalendarPage` нет Month View, в `InsightsPage` индикаторы не tappable, на `EventListPage` фильтр‑панель и индикатор активных фильтров не соответствуют макету.

Решения по скоупу (получены от пользователя):
- Скоуп — только светлая тема. Тёмная — отдельной задачей позже.
- New/Edit Event уже частично переделан (коммит `5307720`); довести разметку до handoff (в т.ч. `Save` снизу sticky‑баром, а не в `ToolbarItems` справа сверху). **Категорически не лезть в нативное поведение Android‑клавиатуры** — прошлая попытка сломала UX.
- Мерджа в master до финала не будет: всё кладём на текущей ветке `maui_redesign`, один PR в конце.

Цель: итеративно (фундамент → компоненты → экраны) привести UI к макету без регрессов в навигации, биндингах и работе с клавиатурой.

---

## Roadmap

### Этап 0. Подготовка (read‑only, без коммита)
- Снять скриншоты текущих экранов (debug‑apk) для до/после.
- Прогнать `dotnet build Pdmt.Maui/Pdmt.Maui.csproj` — зафиксировать baseline.
- Сверить `index.html` (`Pdmt Design System/design_handoff_pdmt_maui/index.html`) — открыть в браузере и пройтись по всем экранам прототипа.

### Этап 1. Design tokens (фундамент)
**Файлы:** `Pdmt.Maui/Resources/Styles/Colors.xaml`, `Pdmt.Maui/Resources/Styles/Styles.xaml`.

- Перенести в `Colors.xaml` все light‑токены из README (Background, Surface, Card, SurfaceVariant, Border, OnSurface, OnSurfaceVariant, Muted, Primary `#006a60`, PrimaryContainer `#cde8e1`, OnPrimary, PositiveBg/Border/Text/Bar, NegativeBg/Border/Text/Bar, Error, Amber). Старые ключи (`Primary=#1565C0`, `Positive`, `Negative`, `Surface`, `Border`, `Muted`, `OnSurface`, `Secondary`, `PrimaryDark`) **не удалять** — переопределить значениями из дизайна, чтобы не ломать существующие биндинги.
- Подключить шрифт **Roboto** (400/500/600/700) через `MauiProgram.cs` → `ConfigureFonts` (`Resources/Fonts/`); зарегистрировать алиас `RobotoRegular/Medium/SemiBold/Bold`.
- В `Styles.xaml` ввести «семантические» стили под токены типографики и переиспользуемые контролы (см. этап 2). Не трогать дефолтные сеттеры существующих стилей до того, как соответствующий экран будет переписан, — иначе ломается всё разом.

**Verification:** проект собирается; `LoginPage` уже подсасывает новый `Primary`/`PrimaryContainer` (визуальное смещение допустимо до этапа 3).

### Этап 2. Reusable styles & компоненты
**Файлы:** `Pdmt.Maui/Resources/Styles/Styles.xaml` + по необходимости новые `ContentView` в `Pdmt.Maui/Views/Controls/`.

Завести как `Style x:Key=...`:
- `H1AppBar` (18/500), `SectionLabel` (11/600 uppercase letter‑spacing 0.07em через `CharacterSpacing`), `Body14`, `Meta12`, `Timestamp10`, `Badge11`.
- `CardSurface` (`Border` со `StrokeShape RoundRectangle 10`, `StrokeThickness 1`, `BackgroundColor Card`).
- `PrimaryFilledButton` (full‑width, 14 radius, Primary bg, OnPrimary text).
- `PillButton` (100 radius — для type chips).
- `ExtendedFab` (Border 14 radius, height 48, центр по горизонтали, `Primary` bg, иконка + лейбл).
- `OutlinedIconButton` 28×28, hover‑состояния через `VisualStateManager` (Edit — индиго, Delete — red).
- `SegmentedControl` — простая `Border` + 2 `Button`/`Frame` с биндингом активной вкладки (Calendar Week/Month, Events type toggle).
- `IconCircleButton` 32×32 для пагинации по периоду в Calendar.
- Сделать helper `BottomSheetSurface` (Border со `StrokeShape RoundRectangle 24,24,0,0`) для Login.

**Verification:** dummy‑страница в Debug, проверяем визуал каждого стиля, отдельно ничего не ломая.

### Этап 3. LoginPage
**Файлы:** `Pdmt.Maui/Views/LoginPage.xaml`, `LoginPage.xaml.cs`.

- Двух­зонная разметка: верх `PrimaryContainer` (логотип 56px + «Pdmt» 26/700 + tagline 13 muted, центрирован), низ — bottom‑sheet `Background` с drag‑handle 36×4 `Border`, заголовком «Sign in», полями Email/Password, full‑width filled кнопкой и текст‑линком «No account? Register».
- Использовать существующие команды `LoginCommand` и т.п. из `LoginViewModel` без правок логики.
- Вставить SVG‑лого как `FontImageSource` или экспортировать в `Resources/Images/logo.svg`.

**Verification:** запустить эмулятор Android, прогнать happy path (логин с валидными credentials → переход на Events).

### Этап 4. EventListPage
**Файлы:** `Pdmt.Maui/Views/EventListPage.xaml(.cs)`, `EventListViewModel.cs` (только если нужны новые свойства), `EventItemViewModel.cs` (только display‑свойства, согласно конвенции CLAUDE.md).

- App bar: Shell `TitleView` с лого 24px + «Events» + filter `ImageButton` справа; красная точка‑бэйдж видима по `HasActiveFilters` (новое computed‑property в VM).
- Свернуть/раскрыть филтр‑панель через `IsVisible` биндинг (без анимаций, чтобы не ломать жесты). Внутри: chips All/Positive/Negative, два `DatePicker` From/To, `FlexLayout` тег‑pill’ов, текст‑линк «Reset filters» — видимость по `HasActiveFilters`.
- Карточка события: `Border` с цветной 3px‑полосой слева (отдельный `BoxView` в `Grid` колонке 0). Переиспользовать существующий `EventItemViewModel.IsPositive` для выбора `posBg/posBorder/posText/posBar` или `negBg/...`. Edit/Delete — `OutlinedIconButton` из этапа 2; теги — bind на `Tags`, отображать через `BindableLayout` с шаблоном `Badge`.
- FAB → `ExtendedFab` («+ New event») в `Grid.Row="*"` поверх списка через `AbsoluteLayout` или `Grid` с одним рядом.

**Не трогать:** команды `EditCommand`, `DeleteCommand`, фильтр‑логику в VM (только биндинги и структуру XAML).

**Verification:** список рендерится, фильтры применяются, edit→edit page, delete мгновенный, FAB переходит на New Event.

### Этап 5. New / Edit Event
**Файлы:** `Pdmt.Maui/Views/NewEventPage.xaml`, `EditEventPage.xaml` и code‑behind, при необходимости — XAML‑правки `EventFormViewModel`.

- Убрать `ToolbarItem Text="Save"` (правый верхний угол) — заменить на **sticky bottom bar**: `Grid` row `Auto` снизу с верхней `BoxView` 1px `Border` и full‑width filled кнопкой «Log event» / «Save changes». Disabled state — `SurfaceVariant` bg, `Muted` text.
- Type toggle — 2 колонки 8px gap, цвета по handoff (Positive `#dcfce7/#86efac/#16a34a`, Negative `#fee2e2/#fca5a5/#dc2626`, Inactive `SurfaceVariant`).
- Intensity — 10 сегментов (1px border‑radius внутри handoff не уточняется, оставляем 4px), цвет по типу, прозрачность растёт с value. Тап = `TapGestureRecognizer` на `BoxView`.
- Tags input: `Entry` + кнопка «Add», Enter/comma добавляют (на `Completed` + `TextChanged` watch).
- Context = `Entry`, Description = `Editor` (3 row).
- «Can influence» — inline `Switch`.
- **Клавиатура:** ничего нативного не переопределять. Никакого custom `EditorHandler`/`EntryHandler`/IME‑интерсепта/`SoftInputExtensions`. Использовать только встроенный `KeyboardScrollManager` MAUI; страницу строить как `Grid Auto,*` где `*` — `ScrollView` с формой, `Auto` снизу — sticky‑бар. Это сохранит поведение, к которому привыкли пользователи Android.
- Существующая `entry_background.xml` (Samsung One UI fix) остаётся как есть.

**Verification:** ручное тестирование в эмуляторе и на реальном Samsung устройстве, если доступно. Обязательно проверить: фокус на каждом поле — клавиатура поднимает scroll до поля, sticky‑бар не наезжает; back‑нажатие закрывает клавиатуру штатно; тап вне поля не делает ничего лишнего.

### Этап 6. Calendar — Week + Month
**Файлы:** `Pdmt.Maui/Views/WeeklyCalendarPage.xaml(.cs)`, `WeeklyCalendarViewModel.cs`, новые VM при необходимости (`CalendarMonthViewModel`).

- App bar: `Shell.TitleView` «Calendar».
- Sub‑header: `IconCircleButton` назад/вперёд (32×32), period label по центру (13/600), `SegmentedControl` Week/Month справа.
- Week View — оставить существующую разметку как «main», только привести цвета/радиусы/типографику к токенам (README прямо говорит treat existing as main).
- Month View — **новый**: `Grid` 7 колонок 3px gap, `minHeight` 52, `RoundRectangle 6`, окраска по среднему скору дня (`posBg`/`negBg`/`Card`), номер даты top‑left 14/600, точки событий 4×4 по центру, intensity bar 3px снизу. Сделать через `BindableLayout` или `CollectionView ItemsLayout=GridItemsLayout(7)`.
- Monthly summary card — `Border` 12 radius `Card` bg, лейбл «APRIL SUMMARY», 2 колонки stat‑cards (positive/negative), Best day / Worst day.

**Сервис:** уточнить, есть ли в `AnalyticsService` метод под месячные данные. Если только weekly — сделать минимальную правку VM, **не** выносить логику в API. Если нужен новый эндпоинт — это вне скоупа редизайна, временно показать «coming soon» state.

**Verification:** Week → Month переключение, навигация по периодам, тап по дню (Month) — пока no‑op (как в прототипе).

### Этап 7. InsightsPage
**Файлы:** `Pdmt.Maui/Views/InsightsPage.xaml(.cs)`, `Pdmt.Maui/Views/InsightCardTemplateSelector.cs`, шаблоны в `Views/InsightCards/`.

- Period chips (Week / 2 weeks / Month) сверху + counter справа.
- Insight card: `Border` 14 radius, category badge (10/700 uppercase coloured pill), title 15/600, description 12/muted, опциональный bar chart.
- Использовать существующий `CarouselView` + `IndicatorView` (см. handoff Option 1). Сделать **tappable dots**: заменить `IndicatorView` на `BindableLayout` из `BoxView`/`ImageButton` 24×24 hit area с `TapGestureRecognizer`, биндить `Position` `CarouselView` через `TwoWay` биндинг к VM.
- Сохранить lifecycle: `OnDisappearing` → `CancelLoad()` (см. CLAUDE.md, конвенция Pdmt.Maui).

**Verification:** свайп вправо/влево листает карточки; тап по точке прыгает на индекс; period chips меняют данные; уход со страницы отменяет HTTP запросы.

### Этап 8. AccountPage
**Файлы:** `Pdmt.Maui/Views/AccountPage.xaml(.cs)`.

- Аватар 72×72 круг `PrimaryContainer` bg, инициал 28/600 `OnPrimary`.
- Email + member since.
- Stats list: 4 строки (Total / Positive / Negative / Tags), правая колонка — число.
- Logout — `OutlinedButton` snizu.
- Существующие команды `AccountViewModel` не менять.

**Verification:** вход, выход, отображение stats.

### Этап 9. Глобальная полировка
- Status bar — `MauiProgram` или `Platforms/Android/MainActivity`: цвет статус‑бара = `Surface`, иконки тёмные. **Не трогать** Window soft input mode (см. этап 5).
- Splash — оставить текущий, только перекрасить background в `#cde8e1` (PrimaryContainer).
- Удалить мёртвые ключи стилей (`PrimaryDark`, `Secondary`), если по grep нигде не используются.
- Финальный прогон unit/integration тестов API не требуется (изменения только в Pdmt.Maui).

### Этап 10. Финал
- Обновить `CLAUDE.md` секцию `Pdmt.Maui Conventions` если появились новые соглашения (новые стили, контролы, подход к sticky save bar).
- Сделать одиночный squashed‑коммит/серию атомарных коммитов и **один PR** `maui_redesign → master`.

---

## Critical files (по этапам)

- `Pdmt.Maui/Resources/Styles/Colors.xaml` — этап 1
- `Pdmt.Maui/Resources/Styles/Styles.xaml` — этапы 1, 2
- `Pdmt.Maui/MauiProgram.cs` — Roboto regis, этап 1
- `Pdmt.Maui/Views/LoginPage.xaml` — этап 3
- `Pdmt.Maui/Views/EventListPage.xaml` + `Pdmt.Maui/ViewModels/EventListViewModel.cs` — этап 4
- `Pdmt.Maui/Views/NewEventPage.xaml`, `EditEventPage.xaml` — этап 5
- `Pdmt.Maui/Views/WeeklyCalendarPage.xaml` + `WeeklyCalendarViewModel.cs` (+ возм. новый `CalendarMonthViewModel`) — этап 6
- `Pdmt.Maui/Views/InsightsPage.xaml`, `InsightCardTemplateSelector.cs`, `InsightCards/*` — этап 7
- `Pdmt.Maui/Views/AccountPage.xaml` — этап 8
- `Pdmt.Maui/Platforms/Android/MainActivity.cs` — статус‑бар, этап 9 (без вмешательств в keyboard)

---

## Re‑use existing utilities

- `EventItemViewModel.IsPositive`, `IntensityColor` — для окраски карточек.
- `InsightCardTemplateSelector` — оставить, добавить новые `DataTemplate` под обновлённый дизайн.
- `entry_background.xml` (Samsung One UI fix, `Platforms/Android/Resources/drawable/`) — не трогать.
- `EventFormViewModel.SaveCommand`, `CanSave` — биндим в новый sticky bar.
- Существующие `IHttpClientFactory`‑based сервисы — без изменений.

---

## Verification (end‑to‑end)

Каждый этап заканчивается ручным прогоном на эмуляторе **+** проверкой регрессов соседних экранов. Критичные сценарии для финального QA:

1. `dotnet build Pdmt.Maui/Pdmt.Maui.csproj` — зелёно.
2. Полный запуск через `Pdmt.Maui` debug на Android emulator (API 33+).
3. Login → Events → New Event (создать) → вернуться → отредактировать → удалить.
4. Calendar: Week ↔ Month, навигация по периоду.
5. Insights: свайп + тап по точкам.
6. Account: logout → Login.
7. **Keyboard sanity:** на каждом поле ввода клавиатура поднимается, scroll корректно, back закрывает клавиатуру, sticky save bar не перекрывает поле. Тестировать на эмуляторе и (если возможно) на Samsung One UI.
8. `build-apk.bat` → release APK собирается.

---

## Out of scope

- Тёмная тема — отдельной итерацией после approve светлой.
- Изменения API (`Pdmt.Api`) — нет, если только Month View не упрётся в недостающий эндпоинт. Тогда временный «coming soon» state, отдельный тикет.
- Pdmt.Client (Blazor) — не трогаем, прототип старый.
- React SPA — отдельный handoff.
