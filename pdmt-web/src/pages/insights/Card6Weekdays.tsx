import { getWeekdayStats } from "@/api/insights";
import type { WeekdayStatDto } from "@/api/types";
import { CardShell, HBar } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

const COLOR_SCALE: [number, string][] = [
  [0.85, "bg-green-500"],
  [0.70, "bg-green-400"],
  [0.55, "bg-lime-400"],
  [0.45, "bg-amber-400"],
  [0.30, "bg-orange-400"],
  [0.15, "bg-red-400"],
  [0.00, "bg-red-500"],
];

function barColor({ posCount, negCount }: WeekdayStatDto): string {
  const total = posCount + negCount;
  if (total === 0) return "bg-slate-300";
  const r = posCount / total;
  return COLOR_SCALE.find(([threshold]) => r >= threshold)![1];
}

export function Card6Weekdays({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch(
    (signal) => getWeekdayStats(range.from, range.to, signal),
    [],
    [range.from, range.to],
    isActive,
  );

  const max = Math.max(...data.map((d) => Math.abs(d.avgIntensity)), 1);

  return (
    <CardShell
      badge="Patterns"
      badgeClass="bg-purple-100 text-purple-700"
      title="Паттерны по дням недели"
      explanation="Средняя интенсивность событий по каждому дню недели. Цвет — соотношение позитивных и негативных событий."
      loading={loading}
      error={error}
      onRetry={retry}
    >
      {data.length === 0 ? (
        <p className="text-sm text-slate-400">Недостаточно данных.</p>
      ) : (
        <div className="flex flex-col gap-2">
          {data.map((d) => (
            <HBar
              key={d.day}
              label={d.day}
              value={d.avgIntensity}
              max={max}
              annotation={
                <span className="flex items-center gap-2">
                  <span>{d.avgIntensity.toFixed(1)}</span>
                  <span className="flex items-center gap-0.5">
                    <span className="text-green-500">{d.posCount}</span>
                    <span className="text-slate-300">/</span>
                    <span className="text-red-400">{d.negCount}</span>
                  </span>
                </span>
              }
              annotationClass="w-24"
              color={barColor(d)}
            />
          ))}
        </div>
      )}
    </CardShell>
  );
}
