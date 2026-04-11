import { getRepeatingTriggers } from "@/api/analytics";
import type { RepeatingTriggerDto } from "@/api/types";
import { CardShell, HBar } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card2Repeating({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error } = useLazyFetch<RepeatingTriggerDto[]>(
    (signal) => getRepeatingTriggers(range.from, range.to, undefined, signal),
    [],
    [range.from, range.to],
    isActive,
  );

  const max = Math.max(...data.map((t) => t.count), 1);

  return (
    <CardShell
      badge="Patterns"
      badgeClass="bg-purple-100 text-purple-700"
      title="Повторяющиеся триггеры"
      explanation="Теги, которые встречались 3 и более раз. Это не случайность — это паттерн."
      loading={loading}
      error={error}
    >
      {data.length === 0 ? (
        <p className="text-sm text-slate-400">Нет повторяющихся тегов за период.</p>
      ) : (
        <div className="flex flex-col gap-2">
          {data.map((t) => (
            <HBar
              key={t.tagName}
              label={t.tagName}
              value={t.count}
              max={max}
              annotation={`×${t.count}`}
              color="bg-purple-400"
            />
          ))}
        </div>
      )}
    </CardShell>
  );
}
