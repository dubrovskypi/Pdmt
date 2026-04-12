import { getWeeklySummary } from "@/api/analytics";
import type { WeeklySummaryDto } from "@/api/types";
import { CardShell } from "./CardShell";
import { useLazyFetch } from "./useLazyFetch";
import type { PeriodRange } from "./types";

export function Card3Balance({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const { data, loading, error, retry } = useLazyFetch<WeeklySummaryDto | null>(
    (signal) => getWeeklySummary(range.weekOf, signal),
    null,
    [range.weekOf],
    isActive,
  );

  return (
    <CardShell
      badge="Balance"
      badgeClass="bg-teal-100 text-teal-700"
      title="Баланс позитивного и негативного"
      explanation="Сколько событий каждого типа и насколько интенсивны."
      loading={loading}
      error={error}
      onRetry={retry}
      weekOnly
    >
      {data && (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col items-center rounded-lg bg-green-50 border border-green-200 p-3">
              <span className="text-xs text-slate-500">Позитивных</span>
              <span className="text-3xl font-bold text-green-700">{data.posCount}</span>
              <span className="text-xs text-slate-500 mt-1">
                ср. интенсивность:{" "}
                <span className="font-medium text-green-700">
                  {data.avgPosIntensity.toFixed(1)}
                </span>
              </span>
            </div>
            <div className="flex flex-col items-center rounded-lg bg-red-50 border border-red-200 p-3">
              <span className="text-xs text-slate-500">Негативных</span>
              <span className="text-3xl font-bold text-red-700">{data.negCount}</span>
              <span className="text-xs text-slate-500 mt-1">
                ср. интенсивность:{" "}
                <span className="font-medium text-red-700">{data.avgNegIntensity.toFixed(1)}</span>
              </span>
            </div>
          </div>
          <div className="flex flex-col gap-1.5">
            <div className="flex gap-2 items-center text-xs">
              <span className="w-24 text-slate-600 flex-shrink-0">Поз. инт.</span>
              <div className="flex-1 bg-slate-100 rounded-full h-2 overflow-hidden">
                <div
                  className="h-2 rounded-full bg-green-400"
                  style={{ width: `${(data.avgPosIntensity / 10) * 100}%` }}
                />
              </div>
              <span className="text-slate-400 w-8 text-right flex-shrink-0">
                {data.avgPosIntensity.toFixed(1)}
              </span>
            </div>
            <div className="flex gap-2 items-center text-xs">
              <span className="w-24 text-slate-600 flex-shrink-0">Нег. инт.</span>
              <div className="flex-1 bg-slate-100 rounded-full h-2 overflow-hidden">
                <div
                  className="h-2 rounded-full bg-red-400"
                  style={{ width: `${(data.avgNegIntensity / 10) * 100}%` }}
                />
              </div>
              <span className="text-slate-400 w-8 text-right flex-shrink-0">
                {data.avgNegIntensity.toFixed(1)}
              </span>
            </div>
          </div>
        </div>
      )}
    </CardShell>
  );
}
