import { getDayOfWeek } from "@/api/analytics";
import type { WeekdayStatsDto } from "@/api/types";
import { CardShell, HBar } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card6DayOfWeek({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch<WeekdayStatsDto[]>(
    (signal) => getDayOfWeek(range.from, range.to, signal),
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
      explanation="Средняя интенсивность событий по каждому дню недели."
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
              annotation={d.avgIntensity.toFixed(1)}
              color={d.avgIntensity >= 0 ? "bg-green-400" : "bg-red-400"}
            />
          ))}
        </div>
      )}
    </CardShell>
  );
}
