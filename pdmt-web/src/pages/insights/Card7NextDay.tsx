import { getNextDayEffects } from "@/api/analytics";
import type { NextDayEffectDto } from "@/api/types";
import { CardShell, HBar } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card7NextDay({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch<NextDayEffectDto[]>(
    (signal) =>
      getNextDayEffects(range.from, range.to, signal).then((r) =>
        [...r].sort((a, b) => Math.abs(b.nextDayAvgScore) - Math.abs(a.nextDayAvgScore)),
      ),
    [],
    [range.from, range.to],
    isActive,
  );

  const max = Math.max(...data.map((t) => Math.abs(t.nextDayAvgScore)), 1);

  return (
    <CardShell
      badge="Next day effect"
      badgeClass="bg-blue-100 text-blue-700"
      title="Эффект следующего дня"
      explanation='Средний балл на следующий день после событий с этим тегом. "После «аргумент» — следующий день в среднем тяжелее."'
      loading={loading}
      error={error}
      onRetry={retry}
    >
      {data.length === 0 ? (
        <p className="text-sm text-slate-400">Недостаточно данных для анализа.</p>
      ) : (
        <div className="flex flex-col gap-2">
          {data.slice(0, 6).map((t) => (
            <HBar
              key={t.tagName}
              label={t.tagName}
              value={t.nextDayAvgScore}
              max={max}
              annotation={t.nextDayAvgScore.toFixed(1)}
              color={t.nextDayAvgScore >= 0 ? "bg-green-400" : "bg-red-400"}
            />
          ))}
        </div>
      )}
    </CardShell>
  );
}
