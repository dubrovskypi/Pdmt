import { getWeeklySummary } from "@/api/analytics";
import type { WeeklySummaryDto } from "@/api/types";
import { CardShell, HBar } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card6DayOfWeek({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch<WeeklySummaryDto | null>(
    (signal) => getWeeklySummary(range.weekOf, signal),
    null,
    [range.weekOf],
    isActive,
  );

  const days = data?.byDayOfWeek ?? [];
  const max = Math.max(...days.map((d) => Math.abs(d.avgIntensity)), 1);

  return (
    <CardShell
      badge="Patterns"
      badgeClass="bg-purple-100 text-purple-700"
      title="Паттерны по дням недели"
      explanation="Средняя интенсивность событий по каждому дню недели."
      loading={loading}
      error={error}
      onRetry={retry}
      weekOnly
    >
      {days.length === 0 ? (
        <p className="text-sm text-slate-400">Недостаточно данных.</p>
      ) : (
        <div className="flex flex-col gap-2">
          {days.map((d) => (
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
