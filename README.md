# Pdmt — Personal Event Tracker

A full-stack application for tracking personal events and analyzing patterns in your life. Log experiences (positive/negative), track metadata, and discover correlations.

## Overview

Pdmt helps you understand yourself better by:
- **Logging events** with intensity, context, tags, and emotional influence
- **Analyzing patterns** across time periods (weekly summaries, trends, correlations)
- **Discovering insights** like repeating triggers, discounted positives, tag interactions, and next-day effects
- **Tracking metrics** like influenceability and day scores

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Clients (Web, Mobile)                    │
├─────────────────────────────────────────────────────────────┤
│  pdmt-web (React + TypeScript)  │  Pdmt.Maui (Android)     │
├─────────────────────────────────────────────────────────────┤
│                    API (ASP.NET Core 8)                     │
│  Controllers → Services → EF Core → PostgreSQL + Redis      │
├─────────────────────────────────────────────────────────────┤
│           Infrastructure (Docker Compose)                    │
│        PostgreSQL  │  Redis  │  Seq (Logging)              │
└─────────────────────────────────────────────────────────────┘
```

## Projects

| Project | Stack | Purpose |
|---------|-------|---------|
| **Pdmt.Api** | ASP.NET Core 8 | REST API backend |
| **Pdmt.Api.Tests** | xUnit + Moq | 127 unit & integration tests |
| **pdmt-web** | React 18 + TypeScript | Production web client |
| **Pdmt.Client** | Blazor WASM | Test/reference UI |
| **Pdmt.Maui** | .NET MAUI | Android mobile app |

## Quick Start

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- Docker & Docker Compose

### Setup

```bash
# Start infrastructure (PostgreSQL, Redis, Seq)
docker compose up -d

# Build & run API
dotnet run --project Pdmt.Api

# Run React SPA (from pdmt-web/)
npm install && npm run dev
```

Visit:
- **API**: https://localhost:7031
- **Web App**: https://localhost:5173
- **Swagger**: https://localhost:7031/swagger/ui

### Testing

```bash
# Run all tests (127 total)
dotnet test Pdmt.Api.Tests/Pdmt.Api.Tests.csproj

# Run specific test class
dotnet test Pdmt.Api.Tests/Pdmt.Api.Tests.csproj --filter "FullyQualifiedName~AuthServiceTests"
```

## API Endpoints

### Authentication
- `POST /api/auth/register` — Register new user
- `POST /api/auth/login` — Login with email/password
- `POST /api/auth/refresh` — Refresh access token
- `POST /api/auth/logout` — Logout (revoke tokens)

### Events
- `GET /api/events` — List events (filterable by date, type, tags, intensity)
- `POST /api/events` — Create event
- `PUT /api/events/{id}` — Update event
- `DELETE /api/events/{id}` — Delete event

### Analytics
- `GET /api/analytics/weekly-summary?date={ISO}` — Weekly overview
- `GET /api/analytics/trends?from={ISO}&to={ISO}&granularity=Week|Month` — Trend data
- `GET /api/analytics/correlations?tagId={id}&from={ISO}&to={ISO}` — Tag correlations
- `GET /api/analytics/calendar/week?date={ISO}` — Calendar week view
- `GET /api/analytics/calendar/month?year=2024&month=1` — Calendar month view

### Insights
All under `/api/analytics/insights/`:
- `repeating-triggers` — Recurring negative patterns
- `discounted-positives` — High-frequency low-impact positives
- `next-day-effects` — How today's events affect tomorrow
- `tag-combos` — Tags that co-occur
- `tag-trend?tagId={id}&granularity=Week|Month` — Tag-specific trends
- `influenceability` — Split of controllable vs uncontrollable negatives

## Key Features

### Authentication & Security
- JWT Bearer tokens (60 min access, 7 day refresh)
- Refresh token rotation on login/refresh
- Rate limiting (Redis fallback to in-memory)
- Role-based data isolation per user

### Data Validation
- UTC normalization at API boundaries
- Nullable reference types enabled
- Post-login async/await throughout

### Testing Coverage
- **Unit tests** (89): Services + rate limiting logic
- **Integration tests** (38): Full HTTP request cycles
- **In-memory database** isolation for fast test runs
- **127 total tests** — all passing

## Development

### Folder Structure
```
Pdmt.Api/
  ├── Controllers/       # Thin HTTP handlers
  ├── Services/          # Business logic
  ├── Domain/            # Entity models
  ├── Dto/               # Request/response contracts
  └── Infrastructure/    # Rate limiting, auth, exceptions

pdmt-web/
  ├── src/
  │   ├── pages/         # Route pages
  │   ├── components/    # Reusable UI
  │   ├── api/           # HTTP client
  │   └── auth/          # JWT + refresh logic
```

## Database

**PostgreSQL** with EF Core Code-First migrations.

Key tables:
- `Users` — Accounts (email, password hash)
- `Events` — Logged experiences (type, intensity, timestamp)
- `Tags` — User-defined categories (per-user, unique by name)
- `EventTags` — Many-to-many join
- `RefreshTokens` — Session tokens (SHA256-hashed)
- `FailedLoginAttempts` — Rate limiting
- `Summaries` — Cached weekly summary snapshots

Indexes on `Events(UserId, Timestamp)` and `Events(UserId, Type)` for fast filtering.

## Monitoring & Logs

- **Seq** (Structured Event Query) at http://localhost:5341 for centralized logging
- Correlation IDs propagated across requests
- Exceptions logged with full stack traces

## Contributing

1. Create a feature branch from `master`
2. Write tests for new logic
3. Run `dotnet test` — ensure 100% pass
4. Commit with conventional commits: `feat:`, `fix:`, `test:`, etc.
5. Submit PR to `master`

## License

Proprietary. All rights reserved.

---

**Built with**: ASP.NET Core 8, React 18, EF Core, PostgreSQL, Redis, .NET MAUI
