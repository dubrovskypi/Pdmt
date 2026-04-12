import { apiGet } from "./client";
import type {
  CalendarWeekDto,
  WeeklySummaryDto,
  TrendPeriodDto,
  RepeatingTriggerDto,
  DiscountedPositiveDto,
  NextDayEffectDto,
  TagComboDto,
  TagTrendPointDto,
  InfluenceabilitySplitDto,
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

export const getTagTrend = (tagId: string, from: string, to: string, signal?: AbortSignal) =>
  apiGet<TagTrendPointDto[]>(
    `/api/analytics/insights/tag-trend?tagId=${encodeURIComponent(tagId)}&from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&period=week`,
    signal,
  );

export const getInfluenceability = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<InfluenceabilitySplitDto>(
    `/api/analytics/insights/influenceability?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );
