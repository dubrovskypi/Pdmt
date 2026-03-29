# ROADMAP.md — Pdmt system design & development plan

## Purpose

Pdmt (Personal Daily Mood Tracker) is a personal event tracking app for gaining an **objective picture** of daily life quality. The core problem: negative events get remembered and amplified, positive ones get forgotten. The app captures both streams without forcing positivity — just measuring reality.

Primary focus: **relationships** as the main source of both irritation and positive experiences. The system must support filtering and analytics by relationship context.

## Current state (Phase 1 — done)

### What exists

- **Pdmt.Api** — ASP.NET Core 8 REST API
  - Controllers → Services → AppDbContext (EF Core)
  - JWT auth (access + refresh tokens, SHA256 hashing, rotation)
  - Rate limiting: composite pattern (Redis → in-memory fallback)
  - Global exception handling middleware
  - CRUD for Events
- **Pdmt.Api.Tests** — xUnit integration tests with in-memory DB and fake auth
- **Pdmt.Client** — Blazor WebAssembly + MudBlazor (test UI for API development)

### Current data model

- **User**: Id, Email, PasswordHash
- **Event**: Id, UserId, Timestamp, Type (Positive/Negative), Category (enum), Intensity (0–10), Context, IsRelationship (bool), Influenceability
- **RefreshToken**: Id, UserId, TokenHash, ExpiresAt, IsRevoked

### Current infrastructure

- Dev: SQL Server LocalDB
- Prod config: PostgreSQL
- Redis: installed as Windows service (rate limiting)

---

## Phase 2 — next (Tags + Docker + MAUI foundation)

### 2.1 Replace Category enum with Tags system

**Why**: Static categories are too rigid. User needs tags like "ссора", "секс", "бар", "литовцы", "работа" — with ability to create new ones on the fly and select from existing.

**Data model changes**:

```
Tag
  - Id (Guid)
  - Name (string, required)
  - UserId (Guid) — tags are per-user
  - CreatedAt (DateTimeOffset)
  
  Unique constraint: (UserId, Name)

EventTag (join table)
  - EventId (Guid)
  - TagId (Guid)
  
  Composite PK: (EventId, TagId)
```

**Migration strategy**:
1. Create Tag and EventTag tables
2. Migrate existing Category enum values into Tag rows for each user
3. Populate EventTag from existing Event.Category
4. Remove Category column from Event
5. Decide on IsRelationship: either keep as a quick filter flag OR replace with a special "отношения" tag. Recommendation: **replace with tag** — keeps filtering logic uniform. Add `IsRelationship` as a computed/virtual property that checks for the tag if needed for backward compat.

**API changes**:
- `GET /api/tags` — list user's tags (with event count per tag)
- `POST /api/tags` — create a new tag
- `DELETE /api/tags/{id}` — delete tag (cascade remove from EventTag)
- `PUT /api/events/{id}` — accept `tagIds[]` instead of `category`
- `POST /api/events` — accept `tagIds[]`; auto-create tags by name if they don't exist (upsert pattern)
- `GET /api/events?tags=tag1,tag2` — filter by tags (AND/OR — start with OR)

**Service logic**:
- `TagService`: CRUD, upsert by name, get-or-create pattern
- `EventService`: update to work with tags instead of category, include tags in queries via `.Include(e => e.EventTags).ThenInclude(et => et.Tag)`

**Indexes**:
- `EventTag(EventId, TagId)` — composite PK covers this
- `Tag(UserId, Name)` — unique index for fast lookup and upsert
- `EventTag(TagId)` — for "all events with tag X" queries

### 2.2 Docker Compose for dev environment

**Why**: Eliminate LocalDB dependency, unify dev/prod on PostgreSQL, simplify onboarding.

**Compose services**:
- `postgres` — PostgreSQL 16, port 5432, volume for data persistence
- `redis` — Redis 7, port 6379
- (optional) `api` — Pdmt.Api containerized, but can also run from IDE against compose services

**Action items**:
1. Create `docker-compose.yml` in solution root
2. Create `Dockerfile` for Pdmt.Api (multi-stage: build + runtime)
3. Switch `appsettings.Development.json` from LocalDB to PostgreSQL (connection string: `Host=localhost;Port=5432;Database=pdmt;Username=pdmt;Password=pdmt_dev`)
4. Update `appsettings.Prod.json` to use environment variables for connection string
5. Add `.env.example` with required environment variables
6. Verify EF migrations work against PostgreSQL

### 2.3 MAUI Android client (foundation)

**Why**: The main use case is quick event logging from phone. Blazor WASM is a test UI.

**Scope for Phase 2** — minimal viable mobile client:
- Login screen (JWT auth, store tokens in SecureStorage)
- Quick add event screen (type toggle, intensity slider, tag selection/creation, optional description)
- Event list with basic filters (date range, tags)
- Pull-to-refresh

**Architecture**:
- Separate project: `Pdmt.Maui`
- HttpClient calls same REST API
- No local database — online only for now (offline is Phase 4)
- MVVM pattern with CommunityToolkit.Mvvm
- API base URL configurable (dev: localhost via Android emulator `10.0.2.2`, prod: hosted URL)

**Local debugging**: Run API locally + Android Emulator in Visual Studio. Emulator accesses host via `10.0.2.2:PORT`.

---

## Phase 3 — analytics, calendar, correlations

### 3.1 Analytics API

**New controller**: `AnalyticsController`  
**New service**: `AnalyticsService`

**Endpoints**:

```
GET /api/analytics/weekly-summary?weekOf=2026-03-16
  Returns:
  - positiveCount, negativeCount, ratio
  - averageIntensity (negative), averageIntensity (positive)
  - topTags: [{tagName, count, avgIntensity}]
  - topPositiveEvents: [{title, intensity, date}]
  - dayOfWeekBreakdown: [{day, negCount, posCount, avgIntensity}]

GET /api/analytics/trends?from=2026-01-01&to=2026-03-24&groupBy=week
  Returns:
  - periods: [{periodStart, positiveCount, negativeCount, avgIntensity}]

GET /api/analytics/correlations?tagId={guid}
  Returns:
  - withTag: {avgIntensity, eventCount, avgDayTension}
  - withoutTag: {avgIntensity, eventCount, avgDayTension}
  - daysOfWeek: [{day, frequency}]
  - timeOfDay: [{hour, frequency}]

GET /api/analytics/calendar/week?weekOf=2026-03-23
  Returns:
  - weekStart, weekEnd
  - days:
    - date
    - positiveCount, negativeCount
    - positiveIntensitySum, negativeIntensitySum
    - dayScore (float) — see "Day score formula" in §3.2
    - topPositiveTags: [{name, count}] (top 2 by frequency)
    - topNegativeTags: [{name, count}] (top 2 by frequency)

GET /api/analytics/calendar/month?month=2026-03
  Returns:
  - days: [{date, positiveCount, negativeCount, dayScore}]
  (lighter payload for month grid — Phase 4)
```

**Implementation**: All queries are EF Core LINQ with `GroupBy`. No raw SQL unless performance requires it. Consider caching weekly summaries (Redis) if queries become slow on large datasets.

### 3.2 Weekly calendar view (primary calendar UI)

**Why**: The core "awareness" tool. Shows the full picture of each week at a glance — not just color, but what happened, how intense it was, and what patterns repeat.

**Design**: Each day is a horizontal card containing:

1. **Day label** (left) — weekday abbreviation + date number.

2. **Horizontal histogram** (center) — a bar stretching in both directions from a center divider:
   - Green bar extends LEFT from center — proportional to total positive intensity sum for the day.
   - Red bar extends RIGHT from center — proportional to total negative intensity sum for the day.
   - Bar width is relative to the max intensity sum across all 7 days (so the worst/best day fills half the track).
   - Event count numbers shown at bar edges (e.g. "3" on the left, "1" on the right).

3. **Top tags** — positioned above/below the histogram:
   - Positive tags (green pills) sit ABOVE the bar, aligned toward center-left.
   - Negative tags (red pills) sit BELOW the bar, aligned toward center-right.
   - Show top 2 most frequent tags per side per day. If a tag appears in multiple events the same day, it shows once.

4. **Day score + mood dot** (right) — a colored dot and a number:
   - Dot color: green (score > 1), red (score < -1), amber (neutral).
   - Number = absolute value of day score.
   - Label: "pos" / "neg" / "even".

5. **Background**: neutral (no mood-colored card backgrounds).

6. **Interaction**: Tap/click a day card → expand to show full event list for that day (with all tags, descriptions, timestamps).

**Day score formula**:

```
dayScore = (sumPositiveIntensities - sumNegativeIntensities) / totalEventCount
```

This means:
- One negative event with intensity 9 outweighs three positive events with intensity 3 each (score = (9 - 9) / 4 = 0 → neutral).
- A day with only negatives at high intensity scores deeply negative.
- A calm day with a few mild positives scores mildly positive.

The formula captures both volume and weight of events, giving a more honest picture than simple event counts.

**API endpoint**: `GET /api/analytics/calendar/week?weekOf=2026-03-23` (see §3.1).

**Navigation**: Previous/Next week buttons. Week starts on Monday.

**Implementation order**: Blazor first (test UI), then MAUI (primary use), then React.

**Monthly calendar view**: deferred to Phase 4. Simpler grid (colored cells only, no histograms), uses the lighter `GET /api/analytics/calendar/month` endpoint. Only useful when there are several months of accumulated data.
Ideas for monthly UI (discuss and implement later):
- Monthly grid, each day colored by dominant mood (green → yellow → red gradient)
- Tap day → see events for that day
- Swipe between months
- 
### 3.3 Correlation insights (carousel)

**Why**: The main "awareness engine" of the app. A dedicated screen with swipeable insight cards — each card answers one specific question about the user's data. No AI, no ML — just SQL aggregations rendered as clear visual statements.

**UI**: Horizontal carousel of cards. Period selector at the top (last week / last 2 weeks / last month). Each card has a colored type badge, a title, a short explanation, and a visualization (horizontal bars, trend bars, or ratio display). Navigation: swipe or prev/next buttons + dot indicators.

**Implementation approach**: All insights are computed on-the-fly by `AnalyticsService`. For a single user with hundreds/low thousands of events, queries run in milliseconds. No background jobs, no snapshot tables. If performance becomes an issue later — add Redis caching with 1-hour TTL.

**Existing API coverage** (maps to current DTOs and methods):

- `GetWeeklySummaryAsync` → `WeeklySummaryDto` provides: PosCount, NegCount, AvgPosIntensity, AvgNegIntensity, TopTags (with count + avg intensity), ByDayOfWeek
- `GetTrendsAsync` → `TrendPeriodDto` provides: per-period PosCount, NegCount, AvgIntensity
- `GetCorrelationsAsync` → `CorrelationsDto` provides: tag-specific AvgIntensityWithTag vs AvgIntensityWithoutTag, DaysOfWeek frequency
- `GetCalendarWeekAsync` → `CalendarWeekDto` provides: per-day scores, tag breakdowns

#### Insight cards (10 total)

**Card 1 — Strongest negative triggers**
- Type badge: "Triggers" (red)
- Shows: Top 5 negative tags ranked by average intensity, plus "all other negative" baseline for comparison.
- Data source: `WeeklySummaryDto.TopTags` filtered by negative events, sorted by AvgIntensity desc.
- Visualization: Horizontal bars with intensity values.

**Card 2 — Repeating triggers**
- Type badge: "Patterns" (purple)
- Shows: Tags that appeared 3+ times in the period. "These aren't random — they're patterns."
- Data source: **New method needed** — `GetRepeatingTriggersAsync(userId, from, to, minCount: 3)`
- Returns: `IReadOnlyList<RepeatingTriggerDto>` where `RepeatingTriggerDto(string TagName, int Count, double AvgIntensity)`
- Visualization: Tag names with frequency bars.

**Card 3 — Positive vs negative balance**
- Type badge: "Balance" (teal)
- Shows: Total positive events vs negative events, plus average intensity comparison showing that negatives feel stronger even when outnumbered.
- Data source: `WeeklySummaryDto.PosCount`, `NegCount`, `AvgPosIntensity`, `AvgNegIntensity`
- Visualization: Large numbers side by side + intensity comparison bars.

**Card 4 — Weekly ratio trend**
- Type badge: "Trends" (blue)
- Shows: How the positive/negative balance shifted week over week (last 6 weeks).
- Data source: `GetTrendsAsync` with `TrendGranularity.Week`
- Visualization: Vertical bars per week, colored green (net positive) or red (net negative).

**Card 5 — Blind spot (discounted positives)**
- Type badge: "Blind spot" (amber)
- Shows: Positive tags with high frequency but low average intensity — things that happen often but the user rates as unimportant. "The good stuff is there — you're just scoring it low."
- Data source: **New method needed** — `GetDiscountedPositivesAsync(userId, from, to)`
- Logic: positive tags where `count >= 5 AND avgIntensity < 4.0`, sorted by count desc.
- Returns: `IReadOnlyList<DiscountedPositiveDto>` where `DiscountedPositiveDto(string TagName, double AvgIntensity, int Count)`
- Visualization: Bars showing low intensity + count annotation.

**Card 6 — Day-of-week patterns**
- Type badge: "Patterns" (purple)
- Shows: Average day score per day of week. "Tuesday is your hardest day, Saturday is your best."
- Data source: `WeeklySummaryDto.ByDayOfWeek` (aggregated across the selected period — may need a period-aware variant).
- Visualization: Horizontal bars per weekday, green (positive score) or red (negative).

**Card 7 — Next day effect**
- Type badge: "Next day effect" (blue)
- Shows: Average day score the day AFTER events with a specific tag. "After 'argument', next day averages -4.1. After 'gym', next day averages +1.8."
- Data source: **New method needed** — `GetNextDayEffectsAsync(userId, from, to)`
- Logic: For each tag that appears 3+ times in the period, compute average dayScore of the following calendar day. Compare to overall average dayScore.
- Returns: `IReadOnlyList<NextDayEffectDto>` where `NextDayEffectDto(string TagName, double NextDayAvgScore, int Occurrences)`
- Visualization: Bars showing next-day score, sorted by absolute impact.

**Card 8 — Tag combinations**
- Type badge: "Combos" (purple)
- Shows: Pairs of tags that frequently appear together in the same day, and how the combination differs from each tag alone.
- Data source: **New method needed** — `GetTagCombosAsync(userId, from, to)`
- Logic: Find tag pairs that co-occur on the same day 3+ times. For each pair, compute average event intensity when both present vs when only one is present.
- Returns: `IReadOnlyList<TagComboDto>` where `TagComboDto(string Tag1, string Tag2, double CombinedAvgIntensity, double Tag1AloneAvgIntensity, double Tag2AloneAvgIntensity, int CoOccurrences)`
- Visualization: Paired bars showing combined vs alone intensity.

**Card 9 — Tag trend over time**
- Type badge: "Trends" (blue)
- Shows: How a specific tag's frequency changes week over week. "Is 'argument' getting better or worse?"
- Data source: **New method needed** — `GetTagTrendAsync(userId, Guid tagId, from, to, TrendGranularity period)`
- Returns: `IReadOnlyList<TagTrendPointDto>` where `TagTrendPointDto(DateTime PeriodStart, int Count, double AvgIntensity)`
- Note: The carousel auto-selects the top 3 most frequent negative tags and shows a trend line for each. User can also pick a tag manually.
- Visualization: Small bar chart showing count per week.

**Card 10 — Influenceability split**
- Type badge: "Control" (teal)
- Shows: What percentage of negative events are things the user can influence vs cannot. Uses the existing `Influenceability` field on Event.
- Data source: **New method needed** — `GetInfluenceabilitySplitAsync(userId, from, to)`
- Logic: Group negative events by Influenceability value. Show counts and average intensity per group.
- Returns: `InfluenceabilitySplitDto(int CanInfluenceCount, double CanInfluenceAvgIntensity, int CannotInfluenceCount, double CannotInfluenceAvgIntensity)`
- Visualization: Two-segment horizontal bar (like a stacked bar) + intensity comparison. The CBT angle: "60% of your negative events are things you can't control — focus energy on the 40% where you have influence."

#### New DTOs needed

```csharp
// Add to Pdmt.Api.Dto.Analytics

public record RepeatingTriggerDto(string TagName, int Count, double AvgIntensity);

public record DiscountedPositiveDto(string TagName, double AvgIntensity, int Count);

public record NextDayEffectDto(string TagName, double NextDayAvgScore, int Occurrences);

public record TagComboDto(
    string Tag1,
    string Tag2,
    double CombinedAvgIntensity,
    double Tag1AloneAvgIntensity,
    double Tag2AloneAvgIntensity,
    int CoOccurrences);

public record TagTrendPointDto(DateTime PeriodStart, int Count, double AvgIntensity);

public record InfluenceabilitySplitDto(
    int CanInfluenceCount,
    double CanInfluenceAvgIntensity,
    int CannotInfluenceCount,
    double CannotInfluenceAvgIntensity);
```

#### New AnalyticsService methods

```csharp
Task<IReadOnlyList<RepeatingTriggerDto>> GetRepeatingTriggersAsync(Guid userId, DateTime from, DateTime to, int minCount = 3);
Task<IReadOnlyList<DiscountedPositiveDto>> GetDiscountedPositivesAsync(Guid userId, DateTime from, DateTime to);
Task<IReadOnlyList<NextDayEffectDto>> GetNextDayEffectsAsync(Guid userId, DateTime from, DateTime to);
Task<IReadOnlyList<TagComboDto>> GetTagCombosAsync(Guid userId, DateTime from, DateTime to);
Task<IReadOnlyList<TagTrendPointDto>> GetTagTrendAsync(Guid userId, Guid tagId, DateTime from, DateTime to, TrendGranularity period);
Task<InfluenceabilitySplitDto> GetInfluenceabilitySplitAsync(Guid userId, DateTime from, DateTime to);
```

#### API endpoints for insights

```
GET /api/analytics/insights/repeating-triggers?from=...&to=...&minCount=3
GET /api/analytics/insights/discounted-positives?from=...&to=...
GET /api/analytics/insights/next-day-effects?from=...&to=...
GET /api/analytics/insights/tag-combos?from=...&to=...
GET /api/analytics/insights/tag-trend?tagId=...&from=...&to=...&period=week
GET /api/analytics/insights/influenceability?from=...&to=...
```

Existing endpoints that also feed the carousel:
```
GET /api/analytics/weekly-summary?weekOf=...       → Cards 1, 3, 6
GET /api/analytics/trends?from=...&to=...&groupBy=week  → Card 4
GET /api/analytics/correlations?tagId=...          → supplementary data for Cards 1, 6
```

#### Implementation order

1. Cards 1, 3, 4, 6 — already covered by existing API, frontend work only.
2. Card 2 (repeating triggers) + Card 5 (blind spot) — simple GROUP BY queries.
3. Card 10 (influenceability) — simple GROUP BY on existing field.
4. Card 7 (next day effect) — self-join with date offset, moderate complexity.
5. Card 8 (tag combos) — co-occurrence join, moderate complexity.
6. Card 9 (tag trend) — variant of existing trends logic filtered by tag.

### 3.4 React SPA (Phase 3)

- Separate project or separate repo
- Consumes same REST API — no backend changes needed
- Start with: login, event list, add event form, analytics dashboard
- Stack: React + TypeScript + fetch (no Redux initially, use React context/state)

---

## Phase 4 — future features

### 4.1 Export for therapist
- `GET /api/export/report?from=...&to=...&format=pdf`
- Weekly/monthly report: stats, top triggers, trends, notable events
- PDF generation server-side (QuestPDF or similar)
- Optional: Markdown export

### 4.2 Notifications / reminders
- MAUI: local notifications at configurable times ("did anything happen in the last 6 hours?")
- Soft, non-intrusive — the goal is awareness, not obligation
- No push notification infrastructure needed — MAUI local notifications are enough

### 4.3 Advanced filters and search
- Full-text search on event descriptions
- Complex filters: tag combinations (AND/OR), intensity range, date range, time of day
- Saved filter presets

### 4.4 Offline support (MAUI)
- SQLite local DB on device
- Sync queue: events created offline are pushed to API when connectivity returns
- Conflict resolution: last-write-wins (user is single, no collaboration)

### 4.5 AI insights (optional, exploratory)
- Periodic analysis of patterns: "this week had significantly more 'ссора' events than usual"
- Anomaly detection on intensity trends
- Could use local LLM or API call — evaluate when the time comes

---

## Architecture decisions (do NOT change)

These decisions are final and should not be reconsidered:

1. **Layered architecture** (Controllers → Services → EF Core) — correct scale for a personal app. No CQRS, no MediatR, no clean architecture project splits.
2. **JWT authentication** — already implemented with refresh token rotation. MAUI uses SecureStorage.
3. **Composite rate limiting** (Redis + in-memory fallback) — good pattern, keep it.
4. **REST API as the single backend** — all clients (Blazor, MAUI, React) consume the same endpoints.
5. **PostgreSQL for both dev and prod** — eliminates provider mismatch issues.
6. **EF Core for all data access** — no raw SQL unless explicitly needed for performance.
7. **Business logic in services only** — controllers remain thin, no try/catch in controllers.

## Free hosting plan

- **API**: Render free tier (or Railway / Fly.io) — containerized ASP.NET Core
- **PostgreSQL**: Render managed PostgreSQL free tier (or Railway)
- **Redis**: Render Redis free tier (or use in-memory fallback only for free hosting — Redis not strictly required if rate limiting volume is low for single user)

## How to use this document with Claude Code

In plan mode, reference specific phases:
- "Read ROADMAP.md Phase 2.1 and implement the Tags system"
- "Read ROADMAP.md Phase 2.2 and create docker-compose.yml"
- "Read ROADMAP.md Phase 3.1 and design the AnalyticsController"

Each phase has enough detail for Claude Code to generate implementation plans and code.