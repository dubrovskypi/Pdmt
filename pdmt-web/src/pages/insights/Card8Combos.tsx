import { getTagCombos } from "@/api/analytics";
import type { TagComboDto } from "@/api/types";
import { CardShell, HBar } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card8Combos({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error } = useLazyFetch<TagComboDto[]>(
    (signal) => getTagCombos(range.from, range.to, signal),
    [],
    [range.from, range.to],
    isActive,
  );

  return (
    <CardShell
      badge="Combos"
      badgeClass="bg-purple-100 text-purple-700"
      title="Комбинации тегов"
      explanation="Пары тегов, которые часто встречаются вместе, и как комбинация отличается от каждого отдельно."
      loading={loading}
      error={error}
    >
      {data.length === 0 ? (
        <p className="text-sm text-slate-400">Нет частых комбинаций тегов за период.</p>
      ) : (
        <div className="flex flex-col gap-3">
          {data.slice(0, 3).map((c, i) => {
            const max = Math.max(
              c.combinedAvgIntensity,
              c.tag1AloneAvgIntensity,
              c.tag2AloneAvgIntensity,
              1,
            );
            return (
              <div key={i} className="flex flex-col gap-1">
                <span className="text-xs font-medium text-slate-700">
                  {c.tag1} + {c.tag2}
                  <span className="text-slate-400 font-normal ml-1">×{c.coOccurrences}</span>
                </span>
                <HBar
                  label="Вместе"
                  value={c.combinedAvgIntensity}
                  max={max}
                  annotation={c.combinedAvgIntensity.toFixed(1)}
                  color="bg-purple-400"
                />
                <HBar
                  label={c.tag1}
                  value={c.tag1AloneAvgIntensity}
                  max={max}
                  annotation={c.tag1AloneAvgIntensity.toFixed(1)}
                  color="bg-slate-400"
                />
                <HBar
                  label={c.tag2}
                  value={c.tag2AloneAvgIntensity}
                  max={max}
                  annotation={c.tag2AloneAvgIntensity.toFixed(1)}
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
