import { getTopTags } from "@/api/analytics";
import type { TriggersDto } from "@/api/types";
import { CardShell, HBar } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card1Triggers({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch<TriggersDto | null>(
    (signal) => getTopTags(range.from, range.to, signal),
    null,
    [range.from, range.to],
    isActive,
  );

  const posMax = Math.max(...(data?.topPosTags.map((t) => t.avgIntensity) ?? []), 1);
  const negMax = Math.max(...(data?.topNegTags.map((t) => t.avgIntensity) ?? []), 1);

  return (
    <CardShell
      badge="Triggers"
      badgeClass="bg-red-100 text-red-700"
      title="Сильнейшие триггеры"
      explanation="Топ тегов по средней интенсивности за период."
      loading={loading}
      error={error}
      onRetry={retry}
    >
      {!data || (data.topPosTags.length === 0 && data.topNegTags.length === 0) ? (
        <p className="text-sm text-slate-400">Недостаточно данных.</p>
      ) : (
        <div className="flex flex-col gap-4">
          {data.topPosTags.length > 0 && (
            <div className="flex flex-col gap-1.5">
              <span className="text-xs font-medium text-green-700">Позитивные</span>
              {data.topPosTags.map((t) => (
                <HBar
                  key={t.tagName}
                  label={t.tagName}
                  value={t.avgIntensity}
                  max={posMax}
                  annotation={`${t.avgIntensity.toFixed(1)}/10`}
                  color="bg-green-400"
                />
              ))}
            </div>
          )}
          {data.topNegTags.length > 0 && (
            <div className="flex flex-col gap-1.5">
              <span className="text-xs font-medium text-red-700">Негативные</span>
              {data.topNegTags.map((t) => (
                <HBar
                  key={t.tagName}
                  label={t.tagName}
                  value={t.avgIntensity}
                  max={negMax}
                  annotation={`${t.avgIntensity.toFixed(1)}/10`}
                  color="bg-red-400"
                />
              ))}
            </div>
          )}
        </div>
      )}
    </CardShell>
  );
}
