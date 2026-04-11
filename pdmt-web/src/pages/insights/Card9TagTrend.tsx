import { useEffect, useState } from "react";
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import { getRepeatingTriggers, getTagTrend } from "@/api/analytics";
import { getTags } from "@/api/tags";
import type { TagTrendPointDto } from "@/api/types";
import { formatWeekRange } from "@/lib/dateUtils";
import { isAbortError, getErrorMessage } from "@/lib/utils";
import { CardShell } from "./CardShell";
import type { PeriodRange } from "./types";

export function Card9TagTrend({ range, isActive }: { range: PeriodRange; isActive: boolean }) {
  const [trend, setTrend] = useState<TagTrendPointDto[]>([]);
  const [tagName, setTagName] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    const controller = new AbortController();
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        const [triggers, allTags] = await Promise.all([
          getRepeatingTriggers(range.from, range.to, undefined, controller.signal),
          getTags(controller.signal),
        ]);
        if (triggers.length === 0) {
          setTrend([]);
          setTagName(null);
          return;
        }
        const top = triggers[0];
        const found = allTags.find((t) => t.name === top.tagName);
        if (!found) {
          setTrend([]);
          setTagName(top.tagName);
          return;
        }
        const data = await getTagTrend(found.id, range.from, range.to, controller.signal);
        setTrend(data);
        setTagName(top.tagName);
      } catch (err: unknown) {
        if (isAbortError(err)) return;
        setError(getErrorMessage(err));
        console.error(err);
      } finally {
        setLoading(false);
      }
    })();
    return () => controller.abort();
  }, [shouldLoad, range.from, range.to]);

  const chartData = trend.map((t) => ({
    week: formatWeekRange(t.periodStart),
    count: t.count,
  }));

  return (
    <CardShell
      badge="Trends"
      badgeClass="bg-blue-100 text-blue-700"
      title="Тренд тега"
      explanation="Как менялась частота самого распространённого тега неделя за неделей."
      loading={loading}
      error={error}
    >
      {trend.length === 0 ? (
        <p className="text-sm text-slate-400">Недостаточно данных для анализа.</p>
      ) : (
        <div className="flex flex-col gap-2">
          {tagName && <span className="text-xs font-medium text-slate-700">«{tagName}»</span>}
          <ResponsiveContainer width="100%" height={160}>
            <BarChart data={chartData} margin={{ top: 4, right: 4, left: -20, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
              <XAxis dataKey="week" tick={{ fontSize: 10 }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 10 }} />
              <Tooltip />
              <Bar dataKey="count" name="Раз в неделю" fill="#93c5fd" radius={[2, 2, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </CardShell>
  );
}
