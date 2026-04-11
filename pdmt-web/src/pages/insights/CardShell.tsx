import React from "react";
import { Button } from "@/components/ui/button";

export interface CardShellProps {
  badge: string;
  badgeClass: string;
  title: string;
  explanation: string;
  loading: boolean;
  error: string | null;
  onRetry?: () => void;
  weekOnly?: boolean;
  children: React.ReactNode;
}

export function CardShell({
  badge,
  badgeClass,
  title,
  explanation,
  loading,
  error,
  onRetry,
  weekOnly,
  children,
}: CardShellProps) {
  return (
    <div className="rounded-xl border bg-white p-5 flex flex-col gap-3 min-h-[300px]">
      <div className={`text-xs font-semibold px-2 py-0.5 rounded-full w-fit ${badgeClass}`}>
        {badge}
      </div>
      <h3 className="text-sm font-semibold text-slate-800">{title}</h3>
      <p className="text-xs text-slate-500">{explanation}</p>
      <div className="flex-1">
        {loading ? (
          <p className="text-sm text-slate-400">Загрузка…</p>
        ) : error ? (
          <div className="flex items-center gap-2 flex-wrap">
            <p className="text-sm text-red-500">{error}</p>
            {onRetry && (
              <Button variant="outline" size="sm" onClick={onRetry}>
                Повторить
              </Button>
            )}
          </div>
        ) : (
          children
        )}
      </div>
      {weekOnly && (
        <p className="text-xs text-slate-400 border-t pt-2 mt-auto">⚠ Данные за текущую неделю</p>
      )}
    </div>
  );
}

export function HBar({
  label,
  value,
  max,
  annotation,
  color = "bg-slate-500",
}: {
  label: string;
  value: number;
  max: number;
  annotation?: string;
  color?: string;
}) {
  const pct = max > 0 ? (Math.abs(value) / max) * 100 : 0;
  return (
    <div className="flex items-center gap-2 text-xs">
      <span className="w-28 truncate flex-shrink-0 text-slate-600">{label}</span>
      <div className="flex-1 bg-slate-100 rounded-full h-2 overflow-hidden">
        <div className={`h-2 rounded-full ${color}`} style={{ width: `${pct}%` }} />
      </div>
      {annotation && (
        <span className="w-12 text-right flex-shrink-0 text-slate-400">{annotation}</span>
      )}
    </div>
  );
}
