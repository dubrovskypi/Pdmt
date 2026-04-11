import { getDiscountedPositives } from "@/api/analytics";
import type { DiscountedPositiveDto } from "@/api/types";
import { CardShell, HBar } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card5BlindSpot({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch<DiscountedPositiveDto[]>(
    (signal) => getDiscountedPositives(range.from, range.to, signal),
    [],
    [range.from, range.to],
    isActive,
  );

  const max = Math.max(...data.map((t) => t.count), 1);

  return (
    <CardShell
      badge="Blind spot"
      badgeClass="bg-amber-100 text-amber-700"
      title="Недооценённые позитивные события"
      explanation="Теги с высокой частотой, но низкой средней интенсивностью. Хорошее есть — просто ты его недооцениваешь."
      loading={loading}
      error={error}
      onRetry={retry}
    >
      {data.length === 0 ? (
        <p className="text-sm text-slate-400">Не найдено недооценённых позитивных тегов.</p>
      ) : (
        <div className="flex flex-col gap-2">
          {data.map((t) => (
            <HBar
              key={t.tagName}
              label={t.tagName}
              value={t.count}
              max={max}
              annotation={`${t.avgIntensity.toFixed(1)}/10`}
              color="bg-amber-300"
            />
          ))}
        </div>
      )}
    </CardShell>
  );
}
