import { apiGet } from "./client";
import type { CalendarWeekDto, WeeklySummaryDto, TrendPeriodDto } from "./types";

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
