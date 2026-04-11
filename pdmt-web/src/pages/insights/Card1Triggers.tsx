import { getWeeklySummary } from "@/api/analytics";
import type { WeeklySummaryDto } from "@/api/types";
import { CardShell, HBar } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card1Triggers({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch<WeeklySummaryDto | null>(
    (signal) => getWeeklySummary(range.weekOf, signal),
    null,
    [range.weekOf],
    isActive,
  );

  const tags = data?.topTags ?? [];
  const max = Math.max(...tags.map((t) => t.avgIntensity), 1);

  return (
    <CardShell
      badge="Triggers"
      badgeClass="bg-red-100 text-red-700"
      title="Сильнейшие триггеры"
      explanation="Топ тегов по средней интенсивности за период."
      loading={loading}
      error={error}
      onRetry={retry}
      weekOnly
    >
      {tags.length === 0 ? (
        <p className="text-sm text-slate-400">Недостаточно данных.</p>
      ) : (
        <div className="flex flex-col gap-2">
          {tags.slice(0, 5).map((t) => (
            <HBar
              key={t.tagName}
              label={t.tagName}
              value={t.avgIntensity}
              max={max}
              annotation={`${t.avgIntensity.toFixed(1)}/10`}
              color="bg-red-400"
            />
          ))}
        </div>
      )}
    </CardShell>
  );
}
