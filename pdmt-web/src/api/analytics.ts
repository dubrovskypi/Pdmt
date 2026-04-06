import { apiGet } from "./client";
import type {
  CalendarWeekDto,
  WeeklySummaryDto,
  TrendPeriodDto,
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
