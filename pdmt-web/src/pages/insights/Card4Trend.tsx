import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import { getTrends } from "@/api/analytics";
import type { TrendPeriodDto } from "@/api/types";
import { formatWeekRange } from "@/lib/dateUtils";
import { CardShell } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card4Trend({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch<TrendPeriodDto[]>(
    (signal) => getTrends(range.from, range.to, signal),
    [],
    [range.from, range.to],
    isActive,
  );

  const chartData = data.map((t) => ({
    week: formatWeekRange(t.periodStart),
    posCount: t.posCount,
    negCount: t.negCount,
  }));

  return (
    <CardShell
      badge="Trends"
      badgeClass="bg-blue-100 text-blue-700"
      title="Тренд соотношения по неделям"
      explanation="Как менялся баланс позитивного и негативного неделя за неделей."
      loading={loading}
      error={error}
      onRetry={retry}
    >
      {data.length === 0 ? (
        <p className="text-sm text-slate-400">Нет данных за период.</p>
      ) : (
        <ResponsiveContainer width="100%" height={180}>
          <BarChart data={chartData} margin={{ top: 4, right: 4, left: -20, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
            <XAxis dataKey="week" tick={{ fontSize: 10 }} />
            <YAxis allowDecimals={false} tick={{ fontSize: 10 }} />
            <Tooltip />
            <Bar dataKey="posCount" name="Позитивных" fill="#4ade80" radius={[2, 2, 0, 0]} />
            <Bar dataKey="negCount" name="Негативных" fill="#f87171" radius={[2, 2, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      )}
    </CardShell>
  );
}
