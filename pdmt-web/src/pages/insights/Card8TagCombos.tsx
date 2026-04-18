import { getTagCombos } from "@/api/insights";
import { CardShell, HBar } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card8TagCombos({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch(
    (signal) => getTagCombos(range.from, range.to, signal),
    [],
    [range.from, range.to],
    isActive,
  );

  return (
    <CardShell
      badge="Tag combos"
      badgeClass="bg-purple-100 text-purple-700"
      title="Комбинации тегов"
      explanation="Пары тегов, которые часто встречаются вместе, и как комбинация отличается от каждого отдельно."
      loading={loading}
      error={error}
      onRetry={retry}
    >
      {data.length === 0 ? (
        <p className="text-sm text-slate-400">Нет частых комбинаций тегов за период.</p>
      ) : (
        <div className="flex flex-col gap-3">
          {data.slice(0, 3).map((c, i) => {
            const max: number = Math.max(
              Math.abs(c.combinedAvgScore),
              Math.abs(c.tag1AloneAvgScore),
              Math.abs(c.tag2AloneAvgScore),
              0.1,
            );
            const fmt = (v: number) => (v >= 0 ? "+" : "") + v.toFixed(1);
            return (
              <div key={i} className="flex flex-col gap-1">
                <span className="text-xs font-medium text-slate-700">
                  {c.tag1} + {c.tag2}
                  <span className="text-slate-400 font-normal ml-1">×{c.coOccurrences}</span>
                </span>
                <HBar
                  label="Вместе"
                  value={Math.abs(c.combinedAvgScore)}
                  max={max}
                  annotation={fmt(c.combinedAvgScore)}
                  color="bg-purple-400"
                />
                <HBar
                  label={c.tag1}
                  value={Math.abs(c.tag1AloneAvgScore)}
                  max={max}
                  annotation={fmt(c.tag1AloneAvgScore)}
                  color="bg-slate-400"
                />
                <HBar
                  label={c.tag2}
                  value={Math.abs(c.tag2AloneAvgScore)}
                  max={max}
                  annotation={fmt(c.tag2AloneAvgScore)}
                  color="bg-slate-400"
                />
              </div>
            );
          })}
        </div>
      )}
    </CardShell>
  );
}
