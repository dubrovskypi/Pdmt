import { apiGet } from "./client";
import type { CalendarWeekDto } from "./types";

export const getCalendarWeek = (weekOf: string) =>
  apiGet<CalendarWeekDto>(
    `/api/analytics/calendar/week?weekOf=${encodeURIComponent(weekOf)}`,
  );
