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

export interface TagSummaryDto {
  tagName: string;
  count: number;
  avgIntensity: number;
}

export interface TopEventDto {
  title: string | null;
  intensity: number;
  date: string;
}

export interface DayOfWeekBreakdownDto {
  day: string;
  posCount: number;
  negCount: number;
  avgIntensity: number;
}

export interface WeeklySummaryDto {
  posCount: number;
  negCount: number;
  posToNegRatio: number;
  avgPosIntensity: number;
  avgNegIntensity: number;
  topTags: TagSummaryDto[];
  topPosEvents: TopEventDto[];
  topNegEvents: TopEventDto[];
  byDayOfWeek: DayOfWeekBreakdownDto[];
}

export interface TrendPeriodDto {
  periodStart: string;
  posCount: number;
  negCount: number;
  avgIntensity: number;
}

// --- Insights ---

export interface RepeatingTriggerDto {
  tagName: string;
  count: number;
  avgIntensity: number;
}

export interface DiscountedPositiveDto {
  tagName: string;
  avgIntensity: number;
  count: number;
}

export interface NextDayEffectDto {
  tagName: string;
  nextDayAvgScore: number;
  occurrences: number;
}

export interface TagComboDto {
  tag1: string;
  tag2: string;
  combinedAvgIntensity: number;
  tag1AloneAvgIntensity: number;
  tag2AloneAvgIntensity: number;
  coOccurrences: number;
}

export interface TagTrendPointDto {
  periodStart: string;
  count: number;
  avgIntensity: number;
}

export interface InfluenceabilitySplitDto {
  canInfluenceCount: number;
  canInfluenceAvgIntensity: number;
  cannotInfluenceCount: number;
  cannotInfluenceAvgIntensity: number;
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
