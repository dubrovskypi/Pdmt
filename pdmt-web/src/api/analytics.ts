import { apiGet } from "./client";
import type { CalendarWeekDto, WeeklySummaryDto} from "./types";

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
