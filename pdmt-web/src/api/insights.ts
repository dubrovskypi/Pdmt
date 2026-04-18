import { apiGet } from "./client";
import type {
  MostIntenseTagsDto,
  RepeatingTriggerDto,
  PosNegBalanceDto,
  TrendPeriodDto,
  DiscountedPositiveDto,
  WeekdayStatDto,
  NextDayEffectDto,
  TagComboDto,
  TagTrendSeriesDto,
  InfluenceabilitySplitDto,
} from "./types";

export const getMostIntenseTags = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<MostIntenseTagsDto>(
    `/api/insights/most-intense-tags?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getRepeatingTriggers = (from: string, to: string, minCount = 3, signal?: AbortSignal) =>
  apiGet<RepeatingTriggerDto[]>(
    `/api/insights/repeating-triggers?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&minCount=${minCount}`,
    signal,
  );

export const getBalance = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<PosNegBalanceDto>(
    `/api/insights/balance?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getTrends = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<TrendPeriodDto[]>(
    `/api/insights/trends?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&period=week`,
    signal,
  );

export const getDiscountedPositives = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<DiscountedPositiveDto[]>(
    `/api/insights/discounted-positives?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getWeekdayStats = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<WeekdayStatDto[]>(
    `/api/insights/weekday-stats?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getNextDayEffects = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<NextDayEffectDto[]>(
    `/api/insights/next-day-effects?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getTagCombos = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<TagComboDto[]>(
    `/api/insights/tag-combos?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );

export const getTagTrend = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<TagTrendSeriesDto[]>(
    `/api/insights/tag-trend?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&period=week`,
    signal,
  );

export const getInfluenceability = (from: string, to: string, signal?: AbortSignal) =>
  apiGet<InfluenceabilitySplitDto>(
    `/api/insights/influenceability?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    signal,
  );
