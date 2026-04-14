import { useState, useEffect } from "react";
import {
  ComposedChart,
  Bar,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from "recharts";
import { getWeeklySummary } from "@/api/analytics";
import type { WeeklySummaryDto } from "@/api/types";
import { Button } from "@/components/ui/button";
import {
  getMondayOf,
  addDays,
  toDateString,
  formatShortDate,
} from "@/lib/dateUtils";
import { isAbortError, getErrorMessage } from "@/lib/utils";

// --- WeekSelector ---

interface WeekSelectorProps {
  weekRange: string;
  onPrev: () => void;
  onNext: () => void;
  onToday: () => void;
}

function WeekSelector({ weekRange, onPrev, onNext, onToday }: WeekSelectorProps) {
  return (
    <div className="flex items-center gap-3">
      <Button variant="outline" size="sm" onClick={onPrev}>
        ‹ Пред.
      </Button>
      <span className="text-sm font-medium text-slate-700 flex-1 text-center">{weekRange}</span>
      <Button variant="outline" size="sm" onClick={onNext}>
        След. ›
      </Button>
      <Button variant="ghost" size="sm" onClick={onToday} className="text-slate-400 text-xs">
        Сегодня
      </Button>
    </div>
  );
}

// --- SummaryStatsSection ---

interface StatCardProps {
  label: string;
  count: number;
  avgIntensity: number;
  color: "green" | "red";
}

function StatCard({ label, count, avgIntensity, color }: StatCardProps) {
  const isGreen = color === "green";
  return (
    <div
      className={`rounded-lg border p-4 flex flex-col gap-2 ${
        isGreen ? "bg-green-50 border-green-200" : "bg-red-50 border-red-200"
      }`}
    >
      <span className="text-xs text-slate-500">{label}</span>
      <span
        className={`text-3xl font-bold leading-none ${isGreen ? "text-green-700" : "text-red-700"}`}
      >
        {count}
      </span>
      <span className="text-xs text-slate-500">
        ср. интенсивность:{" "}
        <span className={`font-medium ${isGreen ? "text-green-700" : "text-red-700"}`}>
          {avgIntensity.toFixed(1)}
        </span>
      </span>
    </div>
  );
}

function SummaryStatsSection({ summary }: { summary: WeeklySummaryDto }) {
  return (
    <div className="flex flex-col gap-2">
      <div className="grid grid-cols-2 gap-3">
        <StatCard
          label="Позитивных"
          count={summary.posCount}
          avgIntensity={summary.avgPosIntensity}
          color="green"
        />
        <StatCard
          label="Негативных"
          count={summary.negCount}
          avgIntensity={summary.avgNegIntensity}
          color="red"
        />
      </div>
      <p className="text-xs text-slate-500 text-center">
        соотношение позитивных к негативным:{" "}
        <span className="font-medium text-slate-700">{summary.posToNegRatio.toFixed(2)}</span>
      </p>
    </div>
  );
}

// --- TopTagsSection ---

function TopTagsSection({ summary }: { summary: WeeklySummaryDto }) {
  const tags = summary.topTags.slice(0, 5);
  if (tags.length === 0) return null;

  const maxCount = Math.max(...tags.map((t) => t.count));

  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-sm font-semibold text-slate-700">Топ теги (по количеству)</h3>
      {tags.map((tag) => (
        <div key={tag.tagName} className="flex items-center gap-2">
          <span className="text-xs text-slate-600 w-24 truncate flex-shrink-0">{tag.tagName}</span>
          <div
            className="flex-1 bg-slate-100 rounded-full h-2 overflow-hidden cursor-default"
            title={`${tag.count} событий`}
          >
            <div
              className="h-2 rounded-full bg-slate-500 transition-all"
              style={{ width: `${(tag.count / maxCount) * 100}%` }}
            />
          </div>
          <span className="text-xs text-slate-400 w-10 text-right flex-shrink-0">
            {tag.avgIntensity.toFixed(1)}/10
          </span>
        </div>
      ))}
    </div>
  );
}

// --- TopEventsSection ---

function TopEventsSection({ summary }: { summary: WeeklySummaryDto }) {
  if (summary.topPosEvents.length === 0 && summary.topNegEvents.length === 0) return null;

  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-sm font-semibold text-slate-700">Топ события (по интенсивности)</h3>
      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-1.5">
          <span className="text-xs font-medium text-green-700">Позитивные</span>
          {summary.topPosEvents.map((ev, i) => (
            <div key={i} className="flex items-start gap-1.5 text-xs">
              <span className="font-medium text-green-600 flex-shrink-0">{ev.intensity}/10</span>
              <span className="text-slate-700 truncate">{ev.title ?? "—"}</span>
              <span className="text-slate-400 flex-shrink-0 ml-auto">
                {formatShortDate(ev.date)}
              </span>
            </div>
          ))}
        </div>
        <div className="flex flex-col gap-1.5">
          <span className="text-xs font-medium text-red-700">Негативные</span>
          {summary.topNegEvents.map((ev, i) => (
            <div key={i} className="flex items-start gap-1.5 text-xs">
              <span className="font-medium text-red-600 flex-shrink-0">{ev.intensity}/10</span>
              <span className="text-slate-700 truncate">{ev.title ?? "—"}</span>
              <span className="text-slate-400 flex-shrink-0 ml-auto">
                {formatShortDate(ev.date)}
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// --- DayOfWeekSection ---

function DayOfWeekTooltip({
  active,
  payload,
}: {
  active?: boolean;
  payload?: Array<{ name: string; value: number }>;
}) {
  if (!active || !payload) return null;
  return (
    <div className="bg-white border border-slate-200 rounded px-2 py-1 text-xs shadow-sm">
      {payload.map((entry, i) => (
        <div
          key={i}
          style={{
            color: entry.name === "Ср. интенсивность" ? "#94a3b8" : "#666",
          }}
        >
          {entry.name}: {entry.name === "Ср. интенсивность" ? entry.value.toFixed(1) : entry.value}
        </div>
      ))}
    </div>
  );
}

function DayOfWeekSection({ summary }: { summary: WeeklySummaryDto }) {
  if (summary.byDayOfWeek.length === 0) return null;

  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-sm font-semibold text-slate-700">По дням недели</h3>
      <ResponsiveContainer width="100%" height={200}>
        <ComposedChart
          data={summary.byDayOfWeek}
          margin={{ top: 4, right: 24, left: -16, bottom: 0 }}
        >
          <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
          <XAxis dataKey="day" tick={{ fontSize: 11 }} />
          <YAxis yAxisId="left" allowDecimals={false} tick={{ fontSize: 11 }} />
          <YAxis yAxisId="right" orientation="right" domain={[0, 10]} tick={{ fontSize: 11 }} />
          <Tooltip content={<DayOfWeekTooltip />} />
          <Legend wrapperStyle={{ fontSize: 12 }} />
          <Bar
            yAxisId="left"
            dataKey="posCount"
            name="Позитивных"
            fill="#4ade80"
            radius={[2, 2, 0, 0]}
          />
          <Bar
            yAxisId="left"
            dataKey="negCount"
            name="Негативных"
            fill="#f87171"
            radius={[2, 2, 0, 0]}
          />
          <Line
            yAxisId="right"
            dataKey="avgIntensity"
            name="Ср. интенсивность"
            type="monotone"
            stroke="#94a3b8"
            strokeWidth={2}
            dot={{ r: 3 }}
          />
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  );
}

// --- AnalyticsPage ---

export function AnalyticsPage() {
  const [weekDate, setWeekDate] = useState<Date>(new Date());
  const [summary, setSummary] = useState<WeeklySummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);

  const monday = getMondayOf(weekDate);
  const sunday = addDays(monday, 6);
  const weekRange = `${monday.toLocaleDateString("ru-RU", { day: "numeric", month: "short" })} — ${sunday.toLocaleDateString("ru-RU", { day: "numeric", month: "short" })}`;

  useEffect(() => {
    const controller = new AbortController();
    const mon = getMondayOf(weekDate);

    void (async () => {
      setLoading(true);
      setError(null);
      try {
        const weekOf = toDateString(mon);
        const summaryData = await getWeeklySummary(weekOf, controller.signal);
        setSummary(summaryData);
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

  return (
    <div className="flex flex-col gap-5">
      <WeekSelector
        weekRange={weekRange}
        onPrev={() => setWeekDate((d) => addDays(d, -7))}
        onNext={() => setWeekDate((d) => addDays(d, 7))}
        onToday={() => setWeekDate(new Date())}
      />

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
      ) : summary ? (
        <>
          <SummaryStatsSection summary={summary} />
          <TopTagsSection summary={summary} />
          <TopEventsSection summary={summary} />
          <DayOfWeekSection summary={summary} />
        </>
      ) : null}
    </div>
  );
}
