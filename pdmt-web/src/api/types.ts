// Mirrors C# DTOs exactly. ASP.NET Core serializes PascalCase → camelCase by default.

export const EventType = { Negative: 0, Positive: 1 } as const;
export type EventTypeValue = (typeof EventType)[keyof typeof EventType];

export const CONTEXT_OPTIONS = [
  "Work",
  "Home",
  "Street",
  "Transport",
  "Other",
] as const;
export type ContextOption = (typeof CONTEXT_OPTIONS)[number];

// --- Tags ---

export interface TagResponseDto {
  id: string;
  name: string;
  createdAt: string;
  eventCount: number;
}

// --- Events ---

export interface EventResponseDto {
  id: string;
  timestamp: string;
  type: EventTypeValue;
  intensity: number;
  title: string;
  description: string | null;
  context: string | null;
  canInfluence: boolean;
  tags: TagResponseDto[];
}

export interface CreateEventDto {
  timestamp: string;
  type: EventTypeValue;
  intensity: number;
  title: string;
  description?: string;
  context?: string;
  canInfluence: boolean;
  tagNames: string[];
}

export interface UpdateEventDto {
  timestamp: string;
  type: EventTypeValue;
  intensity: number;
  title: string;
  description?: string;
  context?: string;
  canInfluence: boolean;
  tagNames: string[];
}

// --- Auth ---

// Used by AuthController (/api/auth/*) — MAUI/Blazor only; React ignores refreshToken
export interface AuthResultDto {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
}

// Used by WebAuthController (/api/auth/web/*) — React SPA
export interface WebAuthResultDto {
  accessToken: string;
  accessTokenExpiresAt: string;
}

// --- Analytics ---

export interface TagCountDto {
  name: string;
  count: number;
}

export interface CalendarDayDetailsDto {
  date: string;
  posCount: number;
  negCount: number;
  positiveIntensitySum: number;
  negativeIntensitySum: number;
  dayScore: number;
  topPositiveTags: TagCountDto[];
  topNegativeTags: TagCountDto[];
}

export interface CalendarWeekDto {
  weekStart: string;
  weekEnd: string;
  days: CalendarDayDetailsDto[];
}
