import { apiGet } from "./client";
import type {
  CalendarWeekDto,
  WeeklySummaryDto,
  TrendPeriodDto,
  RepeatingTriggerDto,
  DiscountedPositiveDto,
  NextDayEffectDto,
  TagComboDto,
  TagTrendSeriesDto,
  InfluenceabilitySplitDto,
  TriggersDto,
  BalanceDto,
  WeekdayStatsDto,
} from "./types";

export const getCalendarWeek = (weekOf: string, signal?: AbortSignal) =>
  apiGet<CalendarWeekDto>(
    `/api/analytics/calendar/week?weekOf=${encodeURIComponent(weekOf)}`,
    signal,
  );

export const getWeeklySummary = (weekOf: string, signal?: AbortSignal) =>
  apiGet<WeeklySummaryDto>(
    `/api/analytics/weekly-summary?weekOf=${encodeURIComponent(weekOf)}`,
    signal,
  );

export const getTrends = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<TrendPeriodDto[]>(
    `/api/analytics/trends?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&period=week`,
    signal,
  );

export const getRepeatingTriggers = (from: string, to: string, minCount = 3, signal?: AbortSignal) =>
  apiGet<RepeatingTriggerDto[]>(
    `/api/analytics/insights/repeating-triggers?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&minCount=${minCount}`,
    signal,
  );

export const getDiscountedPositives = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<DiscountedPositiveDto[]>(
    `/api/analytics/insights/discounted-positives?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getNextDayEffects = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<NextDayEffectDto[]>(
    `/api/analytics/insights/next-day-effects?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getTagCombos = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<TagComboDto[]>(
    `/api/analytics/insights/tag-combos?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getTagTrend = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<TagTrendSeriesDto[]>(
    `/api/analytics/insights/tag-trend?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&period=week`,
    signal,
  );

export const getInfluenceability = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<InfluenceabilitySplitDto>(
    `/api/analytics/insights/influenceability?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getTopTags = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<TriggersDto>(
    `/api/analytics/insights/top-tags?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getBalance = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<BalanceDto>(
    `/api/analytics/insights/balance?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getDayOfWeek = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<WeekdayStatsDto[]>(
    `/api/analytics/insights/day-of-week?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );
