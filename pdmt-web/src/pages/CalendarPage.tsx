import { useState, useEffect } from "react";
import { getCalendarWeek } from "@/api/analytics";
import { getEvents } from "@/api/events";
import type { CalendarDayDetailsDto, EventResponseDto } from "@/api/types";
import { EventType } from "@/api/types";
import { Button } from "@/components/ui/button";
import { getMondayOf, addDays, toDateString, formatDayDisplay } from "@/lib/dateUtils";
import { isAbortError, getErrorMessage } from "@/lib/utils";

// --- Score display ---

type ScoreData = { color: string; dot: string; label: string };

function getScoreData(score: number): ScoreData {
  if (score > 1) return { color: "text-green-600", dot: "bg-green-400", label: "pos" };
  if (score < -1) return { color: "text-red-600", dot: "bg-red-400", label: "neg" };
  return { color: "text-amber-500", dot: "bg-amber-400", label: "even" };
}

// --- Day card ---

interface DayCardProps {
  day: CalendarDayDetailsDto;
  maxIntensitySum: number;
  expanded: boolean;
  events: EventResponseDto[];
  onToggle: () => void;
}

function DayCard({ day, maxIntensitySum, expanded, events, onToggle }: DayCardProps) {
  const { dayName, day: dayNum } = formatDayDisplay(day.date);
  const scoreData = getScoreData(day.dayScore);

  const half = maxIntensitySum > 0 ? 50 : 0;
  const posWidth = maxIntensitySum > 0 ? (day.positiveIntensitySum / maxIntensitySum) * half : 0;
  const negWidth = maxIntensitySum > 0 ? (day.negativeIntensitySum / maxIntensitySum) * half : 0;

  const isEmpty = day.posCount === 0 && day.negCount === 0;

  return (
    <button
      type="button"
      onClick={onToggle}
      className="w-full text-left border rounded-lg bg-white hover:bg-slate-50 transition-colors overflow-hidden"
    >
      <div className="flex items-center gap-3 px-3 py-2.5">
        {/* Day label */}
        <div className="w-10 flex-shrink-0 text-center">
          <div className="text-xs text-slate-400 font-medium">{dayName}</div>
          <div className="text-lg font-bold text-slate-800 leading-tight">{dayNum}</div>
        </div>

        {/* Histogram */}
        <div className="flex-1 flex flex-col gap-1">
          {/* Positive tags */}
          {day.topPositiveTags.length > 0 && (
            <div className="flex gap-1 justify-end pr-[50%]">
              {day.topPositiveTags.map((t) => (
                <span
                  key={t.name}
                  className="text-xs px-1.5 py-0 rounded-full bg-green-100 text-green-700 border border-green-200"
                >
                  {t.name}
                </span>
              ))}
            </div>
          )}

          {/* Bars */}
          <div className="flex items-center h-5">
            <div className="w-1/2 flex justify-end items-center gap-1 pr-0.5">
              {day.posCount > 0 && (
                <span className="text-xs text-green-600 font-medium">{day.posCount}</span>
              )}
              <div
                className="h-3 rounded-l-sm bg-green-400 transition-all"
                style={{ width: `${posWidth}%` }}
              />
            </div>

            <div className="w-px h-full bg-slate-300 flex-shrink-0" />

            <div className="w-1/2 flex items-center gap-1 pl-0.5">
              <div
                className="h-3 rounded-r-sm bg-red-400 transition-all"
                style={{ width: `${negWidth}%` }}
              />
              {day.negCount > 0 && (
                <span className="text-xs text-red-600 font-medium">{day.negCount}</span>
              )}
            </div>
          </div>

          {/* Negative tags */}
          {day.topNegativeTags.length > 0 && (
            <div className="flex gap-1 pl-[50%]">
              {day.topNegativeTags.map((t) => (
                <span
                  key={t.name}
                  className="text-xs px-1.5 py-0 rounded-full bg-red-100 text-red-700 border border-red-200"
                >
                  {t.name}
                </span>
              ))}
            </div>
          )}

          {isEmpty && <div className="text-xs text-slate-300 text-center">нет событий</div>}
        </div>

        {/* Day score */}
        <div className="flex-shrink-0 flex flex-col items-center gap-0.5 w-12">
          <div className={`w-2.5 h-2.5 rounded-full ${scoreData.dot}`} />
          <span className={`text-sm font-bold ${scoreData.color}`}>
            {Math.abs(day.dayScore).toFixed(1)}
          </span>
          <span className="text-xs text-slate-400">{scoreData.label}</span>
        </div>
      </div>

      {/* Expanded: event list */}
      {expanded && events.length > 0 && (
        <div className="border-t px-3 py-2 flex flex-col gap-1.5 bg-slate-50">
          {events.map((ev) => (
            <div key={ev.id} className="flex items-start gap-2 text-xs">
              <span
                className={`mt-0.5 font-medium ${
                  ev.type === EventType.Positive ? "text-green-600" : "text-red-600"
                }`}
              >
                {ev.intensity}/10
              </span>
              <div className="flex-1 min-w-0">
                <span className="font-medium text-slate-800">{ev.title}</span>
                {ev.tags.length > 0 && (
                  <span className="text-slate-400 ml-1">
                    {ev.tags.map((t) => t.name).join(", ")}
                  </span>
                )}
                {ev.description && (
                  <p className="text-slate-500 mt-0.5 line-clamp-1">{ev.description}</p>
                )}
              </div>
              <span className="text-slate-400 flex-shrink-0">
                {new Date(ev.timestamp).toLocaleTimeString("ru-RU", {
                  hour: "2-digit",
                  minute: "2-digit",
                })}
              </span>
            </div>
          ))}
        </div>
      )}
      {expanded && events.length === 0 && (
        <div className="border-t px-3 py-2 text-xs text-slate-400 bg-slate-50">Нет событий</div>
      )}
    </button>
  );
}

// --- Main page ---

export function CalendarPage() {
  const [weekDate, setWeekDate] = useState<Date>(new Date());
  const [calendarWeek, setCalendarWeek] = useState<CalendarDayDetailsDto[]>([]);
  const [dayEvents, setDayEvents] = useState<Record<string, EventResponseDto[]>>({});
  const [expandedDay, setExpandedDay] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();

    void (async () => {
      setLoading(true);
      setError(null);
      setExpandedDay(null);
      setDayEvents({});
      try {
        const week = await getCalendarWeek(toDateString(weekDate), controller.signal);
        setCalendarWeek(week.days);
      } catch (err: unknown) {
        if (isAbortError(err)) return;
        setError(getErrorMessage(err));
        console.error(err);
      } finally {
        setLoading(false);
      }
    })();

    return () => controller.abort();
  }, [weekDate, retryKey]);

  async function handleToggleDay(date: string) {
    if (expandedDay === date) {
      setExpandedDay(null);
      return;
    }
    setExpandedDay(date);
    if (dayEvents[date]) return;

    // Extract just date part (handle both "2026-04-05" and "2026-04-05T..." formats)
    const dateStr = date.includes("T") ? toDateString(new Date(date)) : date;

    const evts = await getEvents({
      from: new Date(dateStr + "T00:00:00Z").toISOString(),
      to: new Date(dateStr + "T23:59:59.999Z").toISOString(),
    });
    setDayEvents((prev) => ({ ...prev, [date]: evts }));
  }

  const maxIntensitySum = calendarWeek.reduce(
    (max, d) => Math.max(max, d.positiveIntensitySum, d.negativeIntensitySum),
    0,
  );

  const monday = getMondayOf(weekDate);

  // Build full week with API data or empty days
  const filledWeek: CalendarDayDetailsDto[] = Array.from({ length: 7 }, (_, i) => {
    const dayDate = addDays(monday, i);
    const dateStr = toDateString(dayDate);
    const found = calendarWeek.find((d) => toDateString(new Date(d.date)) === dateStr);

    return (
      found || {
        date: dateStr,
        posCount: 0,
        negCount: 0,
        positiveIntensitySum: 0,
        negativeIntensitySum: 0,
        dayScore: 0,
        topPositiveTags: [],
        topNegativeTags: [],
      }
    );
  });

  const weekRange = `${monday.toLocaleDateString("ru-RU", { day: "numeric", month: "short" })} — ${addDays(monday, 6).toLocaleDateString("ru-RU", { day: "numeric", month: "short" })}`;

  return (
    <div className="flex flex-col gap-4">
      {/* Navigation */}
      <div className="flex items-center gap-3">
        <Button variant="outline" size="sm" onClick={() => setWeekDate((d) => addDays(d, -7))}>
          ‹ Пред.
        </Button>
        <span className="text-sm font-medium text-slate-700 flex-1 text-center">{weekRange}</span>
        <Button variant="outline" size="sm" onClick={() => setWeekDate((d) => addDays(d, 7))}>
          След. ›
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => setWeekDate(new Date())}
          className="text-slate-400 text-xs"
        >
          Сегодня
        </Button>
      </div>

      {/* Calendar */}
      {error && (
        <div className="flex items-center gap-2">
          <p className="text-sm text-red-500 bg-red-50 border border-red-200 rounded px-3 py-2">
            {error}
          </p>
          <Button variant="outline" size="sm" onClick={() => setRetryKey((k) => k + 1)}>
            Повторить
          </Button>
        </div>
      )}
      {loading ? (
        <p className="text-sm text-slate-400">Загрузка…</p>
      ) : (
        <div className="flex flex-col gap-2">
          {filledWeek.map((day) => (
            <DayCard
              key={day.date}
              day={day}
              maxIntensitySum={maxIntensitySum}
              expanded={expandedDay === day.date}
              events={dayEvents[day.date] ?? []}
              onToggle={() => void handleToggleDay(day.date)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
