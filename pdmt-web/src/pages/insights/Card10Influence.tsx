import { getInfluenceability } from "@/api/analytics";
import type { InfluenceabilitySplitDto } from "@/api/types";
import { CardShell } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card10Influence({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error } = useLazyFetch<InfluenceabilitySplitDto | null>(
    (signal) => getInfluenceability(range.from, range.to, signal),
    null,
    [range.from, range.to],
    isActive,
  );

  const total = data ? data.canInfluenceCount + data.cannotInfluenceCount : 0;
  const canPct = total > 0 ? Math.round((data!.canInfluenceCount / total) * 100) : 0;
  const cannotPct = 100 - canPct;

  return (
    <CardShell
      badge="Control"
      badgeClass="bg-teal-100 text-teal-700"
      title="Что в твоей власти"
      explanation="Какой процент негативных событий ты можешь изменить. Сосредоточься на том, что в твоей власти."
      loading={loading}
      error={error}
    >
      {data && total > 0 ? (
        <div className="flex flex-col gap-4">
          <div className="flex rounded-full overflow-hidden h-4">
            <div
              className="bg-teal-400 flex items-center justify-center text-[10px] text-white font-medium"
              style={{ width: `${canPct}%` }}
            >
              {canPct > 15 ? `${canPct}%` : ""}
            </div>
            <div
              className="bg-slate-300 flex items-center justify-center text-[10px] text-slate-600 font-medium"
              style={{ width: `${cannotPct}%` }}
            >
              {cannotPct > 15 ? `${cannotPct}%` : ""}
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3 text-xs">
            <div className="flex flex-col gap-0.5">
              <span className="font-medium text-teal-700">Могу повлиять</span>
              <span className="text-2xl font-bold text-teal-700">{data.canInfluenceCount}</span>
              <span className="text-slate-500">
                ср. инт. {data.canInfluenceAvgIntensity.toFixed(1)}/10
              </span>
            </div>
            <div className="flex flex-col gap-0.5">
              <span className="font-medium text-slate-600">Не могу повлиять</span>
              <span className="text-2xl font-bold text-slate-600">{data.cannotInfluenceCount}</span>
              <span className="text-slate-500">
                ср. инт. {data.cannotInfluenceAvgIntensity.toFixed(1)}/10
              </span>
            </div>
          </div>
        </div>
      ) : (
        !loading && !error && <p className="text-sm text-slate-400">Недостаточно данных.</p>
      )}
    </CardShell>
  );
}
