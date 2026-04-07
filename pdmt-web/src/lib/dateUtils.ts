// UTC-based date utilities shared across Calendar, Analytics, and Insights pages.

export function getMondayOf(d: Date): Date {
  const utcDate = new Date(d.toISOString());
  const day = utcDate.getUTCDay();
  const diff = day === 0 ? -6 : 1 - day;
  const monday = new Date(utcDate);
  monday.setUTCDate(utcDate.getUTCDate() + diff);
  monday.setUTCHours(0, 0, 0, 0);
  return monday;
}

export function addDays(d: Date, n: number): Date {
  const result = new Date(d);
  result.setUTCDate(d.getUTCDate() + n);
  return result;
}

/** Returns "YYYY-MM-DD" from a Date (UTC). */
export function toDateString(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`;
}

/** Returns "DD.MM–DD.MM" week range starting from the given ISO date string. */
export function formatWeekRange(iso: string): string {
  const start = new Date(iso);
  const end = new Date(start);
  end.setUTCDate(start.getUTCDate() + 6);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(start.getUTCDate())}.${pad(start.getUTCMonth() + 1)}–${pad(end.getUTCDate())}.${pad(end.getUTCMonth() + 1)}`;
}

const DAY_NAMES = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

/** Returns day number and short day name (Mon-based) from an ISO date string. */
export function formatDayDisplay(iso: string): { day: string; dayName: string } {
  const d = new Date(iso);
  const dayIndex = (d.getUTCDay() + 6) % 7;
  return { day: String(d.getUTCDate()), dayName: DAY_NAMES[dayIndex] };
}

/** Returns "DD.MM" from an ISO date string (UTC). */
export function formatShortDate(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(d.getUTCDate())}.${pad(d.getUTCMonth() + 1)}`;
}
