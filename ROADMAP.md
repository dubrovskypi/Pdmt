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

GET /api/analytics/calendar?month=2026-03
  Returns:
  - days: [{date, positiveCount, negativeCount, dominantIntensity, colorCode}]
```

**Implementation**: All queries are EF Core LINQ with `GroupBy`. No raw SQL unless performance requires it. Consider caching weekly summaries (Redis) if queries become slow on large datasets.

### 3.2 Calendar view

Frontend feature (Blazor first, then MAUI, then React):
- Monthly grid, each day colored by dominant mood (green → yellow → red gradient)
- Tap day → see events for that day
- Swipe between months

### 3.3 Correlation insights

Show on analytics screen:
- "Events tagged 'ссора' have average intensity 8.2 vs 4.1 for all other events"
- "Mondays have 40% more negative events than average"
- "After events tagged 'бар', next-day intensity averages 6.8"

This is the query logic from `GET /api/analytics/correlations` rendered as cards/charts.

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
