import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import { getTagTrend } from "@/api/insights";
import type { TagTrendSeriesDto } from "@/api/types";
import { formatWeekRange } from "@/lib/dateUtils";
import { CardShell } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

const SERIES_COLORS = ["#93c5fd", "#86efac", "#fca5a5"];

export function Card9TagTrend({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data: series, loading, error, retry } = useLazyFetch<TagTrendSeriesDto[]>(
    (signal) => getTagTrend(range.from, range.to, signal),
    [],
    [range.from, range.to],
    isActive,
  );

  return (
    <CardShell
      badge="Tags trend"
      badgeClass="bg-blue-100 text-blue-700"
      title="Тренд тегов"
      explanation="Как менялась частота трёх самых распространённых тегов неделя за неделей."
      loading={loading}
      error={error}
      onRetry={retry}
    >
      {series.length === 0 ? (
        <p className="text-sm text-slate-400">Недостаточно данных для анализа.</p>
      ) : (
        <div className="flex flex-col gap-4">
          {series.map((s, i) => (
            <div key={s.tagName} className="flex flex-col gap-1">
              <span className="text-xs font-medium text-slate-700">«{s.tagName}»</span>
              <ResponsiveContainer width="100%" height={100}>
                <BarChart data={s.points.map((p) => ({ week: formatWeekRange(p.periodStart), count: p.count }))} margin={{ top: 2, right: 4, left: -20, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                  <XAxis dataKey="week" tick={{ fontSize: 9 }} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 9 }} />
                  <Tooltip />
                  <Bar dataKey="count" name="Раз в неделю" fill={SERIES_COLORS[i % SERIES_COLORS.length]} radius={[2, 2, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          ))}
        </div>
      )}
    </CardShell>
  );
}
