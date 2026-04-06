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

export const getCalendarWeek = (weekOf: string) =>
  apiGet<CalendarWeekDto>(
    `/api/analytics/calendar/week?weekOf=${encodeURIComponent(weekOf)}`,
  );

export const getWeeklySummary = (weekOf: string) =>
  apiGet<WeeklySummaryDto>(
    `/api/analytics/weekly-summary?weekOf=${encodeURIComponent(weekOf)}`,
  );

export const getTrends = (from: string, to: string) =>
  apiGet<TrendPeriodDto[]>(
    `/api/analytics/trends?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&period=week`,
  );

export const getRepeatingTriggers = (from: string, to: string, minCount = 3) =>
  apiGet<RepeatingTriggerDto[]>(
    `/api/analytics/insights/repeating-triggers?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&minCount=${minCount}`,
  );

export const getDiscountedPositives = (from: string, to: string) =>
  apiGet<DiscountedPositiveDto[]>(
    `/api/analytics/insights/discounted-positives?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
  );

export const getNextDayEffects = (from: string, to: string) =>
  apiGet<NextDayEffectDto[]>(
    `/api/analytics/insights/next-day-effects?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
  );

export const getTagCombos = (from: string, to: string) =>
  apiGet<TagComboDto[]>(
    `/api/analytics/insights/tag-combos?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
  );

export const getTagTrend = (tagId: string, from: string, to: string) =>
  apiGet<TagTrendPointDto[]>(
    `/api/analytics/insights/tag-trend?tagId=${encodeURIComponent(tagId)}&from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&period=week`,
  );

export const getInfluenceability = (from: string, to: string) =>
  apiGet<InfluenceabilitySplitDto>(
    `/api/analytics/insights/influenceability?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
  );
