import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import { getTrends } from "@/api/insights";
import { formatWeekRange } from "@/lib/dateUtils";
import { CardShell } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

type BarProps = {
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  avgIntensity?: number;
};

function intensityBar(fill: string, maxAbs: number) {
  return ({ x = 0, y = 0, width = 0, height = 0, avgIntensity = 0 }: BarProps) => {
    const opacity = 0.2 + 0.8 * (Math.abs(avgIntensity) / maxAbs);
    return <rect x={x} y={y} width={width} height={Math.max(height, 0)} fill={fill} fillOpacity={opacity} rx={2} />;
  };
}

export function Card4Trend({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch(
    (signal) => getTrends(range.from, range.to, signal),
    [],
    [range.from, range.to],
    isActive,
  );

  const maxAbs = Math.max(...data.map((t) => Math.abs(t.avgIntensity)), 1);

  const chartData = data.map((t) => ({
    week: formatWeekRange(t.periodStart),
    posCount: t.posCount,
    negCount: t.negCount,
    avgIntensity: t.avgIntensity,
  }));

  return (
    <CardShell
      badge="Trends"
      badgeClass="bg-blue-100 text-blue-700"
      title="Тренд соотношения по неделям"
      explanation="Высота полоски — количество событий. Яркость — средняя интенсивность за неделю."
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
            <Tooltip
              content={({ active, payload, label }) => {
                if (!active || !payload?.length) return null;
                const d = payload[0].payload as (typeof chartData)[0];
                return (
                  <div className="bg-white border border-slate-200 rounded p-2 text-xs shadow">
                    <p className="font-medium text-slate-600 mb-1">{label}</p>
                    <p className="text-green-500">Позитивных: {d.posCount}</p>
                    <p className="text-red-400">Негативных: {d.negCount}</p>
                    <p className="text-slate-400 mt-1">Интенсивность: {d.avgIntensity.toFixed(1)}</p>
                  </div>
                );
              }}
            />
            <Bar dataKey="posCount" name="Позитивных" fill="#4ade80" shape={intensityBar("#4ade80", maxAbs)} />
            <Bar dataKey="negCount" name="Негативных" fill="#f87171" shape={intensityBar("#f87171", maxAbs)} />
          </BarChart>
        </ResponsiveContainer>
      )}
    </CardShell>
  );
}