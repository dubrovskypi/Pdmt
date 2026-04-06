import { useState, useEffect } from "react";
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import {
  getWeeklySummary,
  getTrends,
  getRepeatingTriggers,
  getDiscountedPositives,
  getNextDayEffects,
  getTagCombos,
  getTagTrend,
  getInfluenceability,
} from "@/api/analytics";
import { getTags } from "@/api/tags";
import type {
  WeeklySummaryDto,
  TrendPeriodDto,
  RepeatingTriggerDto,
  DiscountedPositiveDto,
  NextDayEffectDto,
  TagComboDto,
  TagTrendPointDto,
  InfluenceabilitySplitDto,
} from "@/api/types";
import { Button } from "@/components/ui/button";

// --- Types ---

type Period = 7 | 14 | 30;

interface PeriodRange {
  from: string;
  to: string;
  weekOf: string; // Monday of `from` date, for weekly-summary cards
}

// --- Date utilities ---

function getMondayOf(d: Date): Date {
  const date = new Date(d);
  const day = date.getUTCDay();
  const diff = day === 0 ? -6 : 1 - day;
  date.setUTCDate(date.getUTCDate() + diff);
  date.setUTCHours(0, 0, 0, 0);
  return date;
}

function toDateString(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`;
}

function formatWeekRange(iso: string): string {
  const start = new Date(iso);
  const end = new Date(start);
  end.setUTCDate(start.getUTCDate() + 6);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(start.getUTCDate())}.${pad(start.getUTCMonth() + 1)}–${pad(end.getUTCDate())}.${pad(end.getUTCMonth() + 1)}`;
}

function getPeriodRange(period: Period): PeriodRange {
  const to = new Date();
  const from = new Date(to.getTime() - period * 24 * 60 * 60 * 1000);
  return {
    from: from.toISOString(),
    to: to.toISOString(),
    weekOf: toDateString(getMondayOf(from)),
  };
}

// --- PeriodSelector ---

const PERIOD_LABELS: Record<Period, string> = {
  7: "7 дней",
  14: "14 дней",
  30: "30 дней",
};

function PeriodSelector({
  period,
  onChange,
}: {
  period: Period;
  onChange: (p: Period) => void;
}) {
  return (
    <div className="flex gap-1">
      {([7, 14, 30] as Period[]).map((p) => (
        <Button
          key={p}
          size="sm"
          variant={period === p ? "default" : "outline"}
          onClick={() => onChange(p)}
          className="text-xs"
        >
          {PERIOD_LABELS[p]}
        </Button>
      ))}
    </div>
  );
}

// --- CarouselNav ---

function CarouselNav({
  total,
  activeIndex,
  onPrev,
  onNext,
}: {
  total: number;
  activeIndex: number;
  onPrev: () => void;
  onNext: () => void;
}) {
  return (
    <div className="flex items-center justify-between">
      <Button
        variant="outline"
        size="sm"
        className="w-16"
        onClick={onPrev}
        disabled={activeIndex === 0}
      >
        ‹
      </Button>
      <div className="flex gap-1.5">
        {Array.from({ length: total }).map((_, i) => (
          <div
            key={i}
            className={`w-1.5 h-1.5 rounded-full transition-colors ${
              i === activeIndex ? "bg-slate-700" : "bg-slate-300"
            }`}
          />
        ))}
      </div>
      <Button
        variant="outline"
        size="sm"
        className="w-16"
        onClick={onNext}
        disabled={activeIndex === total - 1}
      >
        ›
      </Button>
    </div>
  );
}

// --- CardShell ---

interface CardShellProps {
  badge: string;
  badgeClass: string;
  title: string;
  explanation: string;
  loading: boolean;
  error: string | null;
  weekOnly?: boolean;
  children: React.ReactNode;
}

function CardShell({
  badge,
  badgeClass,
  title,
  explanation,
  loading,
  error,
  weekOnly,
  children,
}: CardShellProps) {
  return (
    <div className="rounded-xl border bg-white p-5 flex flex-col gap-3 min-h-[300px]">
      <div
        className={`text-xs font-semibold px-2 py-0.5 rounded-full w-fit ${badgeClass}`}
      >
        {badge}
      </div>
      <h3 className="text-sm font-semibold text-slate-800">{title}</h3>
      <p className="text-xs text-slate-500">{explanation}</p>
      <div className="flex-1">
        {loading ? (
          <p className="text-sm text-slate-400">Загрузка…</p>
        ) : error ? (
          <p className="text-sm text-red-500">{error}</p>
        ) : (
          children
        )}
      </div>
      {weekOnly && (
        <p className="text-xs text-slate-400 border-t pt-2 mt-auto">
          ⚠ Данные за текущую неделю
        </p>
      )}
    </div>
  );
}

// --- Horizontal bar util ---

function HBar({
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
      <span className="w-28 truncate flex-shrink-0 text-slate-600">
        {label}
      </span>
      <div className="flex-1 bg-slate-100 rounded-full h-2 overflow-hidden">
        <div
          className={`h-2 rounded-full ${color}`}
          style={{ width: `${pct}%` }}
        />
      </div>
      {annotation && (
        <span className="w-12 text-right flex-shrink-0 text-slate-400">
          {annotation}
        </span>
      )}
    </div>
  );
}

// --- Card 1 — Strongest triggers ---

function Card1Triggers({
  range,
  isActive,
}: {
  range: PeriodRange;
  isActive: boolean;
}) {
  const [data, setData] = useState<WeeklySummaryDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        setData(await getWeeklySummary(range.weekOf));
      } catch {
        setError("Не удалось загрузить данные.");
      } finally {
        setLoading(false);
      }
    })();
  }, [shouldLoad, range.weekOf]);

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

// --- Card 2 — Repeating triggers ---

function Card2Repeating({
  range,
  isActive,
}: {
  range: PeriodRange;
  isActive: boolean;
}) {
  const [data, setData] = useState<RepeatingTriggerDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        setData(await getRepeatingTriggers(range.from, range.to));
      } catch {
        setError("Не удалось загрузить данные.");
      } finally {
        setLoading(false);
      }
    })();
  }, [shouldLoad, range.from, range.to]);

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
        <p className="text-sm text-slate-400">
          Нет повторяющихся тегов за период.
        </p>
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

// --- Card 3 — Pos vs Neg balance ---

function Card3Balance({
  range,
  isActive,
}: {
  range: PeriodRange;
  isActive: boolean;
}) {
  const [data, setData] = useState<WeeklySummaryDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        setData(await getWeeklySummary(range.weekOf));
      } catch {
        setError("Не удалось загрузить данные.");
      } finally {
        setLoading(false);
      }
    })();
  }, [shouldLoad, range.weekOf]);

  return (
    <CardShell
      badge="Balance"
      badgeClass="bg-teal-100 text-teal-700"
      title="Баланс позитивного и негативного"
      explanation="Сколько событий каждого типа и насколько интенсивны."
      loading={loading}
      error={error}
      weekOnly
    >
      {data && (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col items-center rounded-lg bg-green-50 border border-green-200 p-3">
              <span className="text-xs text-slate-500">Позитивных</span>
              <span className="text-3xl font-bold text-green-700">
                {data.posCount}
              </span>
              <span className="text-xs text-slate-500 mt-1">
                ср. интенсивность:{" "}
                <span className="font-medium text-green-700">
                  {data.avgPosIntensity.toFixed(1)}
                </span>
              </span>
            </div>
            <div className="flex flex-col items-center rounded-lg bg-red-50 border border-red-200 p-3">
              <span className="text-xs text-slate-500">Негативных</span>
              <span className="text-3xl font-bold text-red-700">
                {data.negCount}
              </span>
              <span className="text-xs text-slate-500 mt-1">
                ср. интенсивность:{" "}
                <span className="font-medium text-red-700">
                  {data.avgNegIntensity.toFixed(1)}
                </span>
              </span>
            </div>
          </div>
          <div className="flex flex-col gap-1.5">
            <div className="flex gap-2 items-center text-xs">
              <span className="w-24 text-slate-600 flex-shrink-0">
                Поз. инт.
              </span>
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
              <span className="w-24 text-slate-600 flex-shrink-0">
                Нег. инт.
              </span>
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

// --- Card 4 — Weekly ratio trend ---

function Card4Trend({
  range,
  isActive,
}: {
  range: PeriodRange;
  isActive: boolean;
}) {
  const [data, setData] = useState<TrendPeriodDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        setData(await getTrends(range.from, range.to));
      } catch {
        setError("Не удалось загрузить данные.");
      } finally {
        setLoading(false);
      }
    })();
  }, [shouldLoad, range.from, range.to]);

  const chartData = data.map((t) => ({
    week: formatWeekRange(t.periodStart),
    posCount: t.posCount,
    negCount: t.negCount,
  }));

  return (
    <CardShell
      badge="Trends"
      badgeClass="bg-blue-100 text-blue-700"
      title="Тренд соотношения по неделям"
      explanation="Как менялся баланс позитивного и негативного неделя за неделей."
      loading={loading}
      error={error}
    >
      {data.length === 0 ? (
        <p className="text-sm text-slate-400">Нет данных за период.</p>
      ) : (
        <ResponsiveContainer width="100%" height={180}>
          <BarChart
            data={chartData}
            margin={{ top: 4, right: 4, left: -20, bottom: 0 }}
          >
            <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
            <XAxis dataKey="week" tick={{ fontSize: 10 }} />
            <YAxis allowDecimals={false} tick={{ fontSize: 10 }} />
            <Tooltip />
            <Bar
              dataKey="posCount"
              name="Позитивных"
              fill="#4ade80"
              radius={[2, 2, 0, 0]}
            />
            <Bar
              dataKey="negCount"
              name="Негативных"
              fill="#f87171"
              radius={[2, 2, 0, 0]}
            />
          </BarChart>
        </ResponsiveContainer>
      )}
    </CardShell>
  );
}

// --- Card 5 — Blind spot ---

function Card5BlindSpot({
  range,
  isActive,
}: {
  range: PeriodRange;
  isActive: boolean;
}) {
  const [data, setData] = useState<DiscountedPositiveDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        setData(await getDiscountedPositives(range.from, range.to));
      } catch {
        setError("Не удалось загрузить данные.");
      } finally {
        setLoading(false);
      }
    })();
  }, [shouldLoad, range.from, range.to]);

  const max = Math.max(...data.map((t) => t.count), 1);

  return (
    <CardShell
      badge="Blind spot"
      badgeClass="bg-amber-100 text-amber-700"
      title="Недооценённые позитивные события"
      explanation="Теги с высокой частотой, но низкой средней интенсивностью. Хорошее есть — просто ты его недооцениваешь."
      loading={loading}
      error={error}
    >
      {data.length === 0 ? (
        <p className="text-sm text-slate-400">
          Не найдено недооценённых позитивных тегов.
        </p>
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

// --- Card 6 — Day of week patterns ---

function Card6DayOfWeek({
  range,
  isActive,
}: {
  range: PeriodRange;
  isActive: boolean;
}) {
  const [data, setData] = useState<WeeklySummaryDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        setData(await getWeeklySummary(range.weekOf));
      } catch {
        setError("Не удалось загрузить данные.");
      } finally {
        setLoading(false);
      }
    })();
  }, [shouldLoad, range.weekOf]);

  const days = data?.byDayOfWeek ?? [];
  const max = Math.max(...days.map((d) => Math.abs(d.avgIntensity)), 1);

  return (
    <CardShell
      badge="Patterns"
      badgeClass="bg-purple-100 text-purple-700"
      title="Паттерны по дням недели"
      explanation="Средняя интенсивность событий по каждому дню недели."
      loading={loading}
      error={error}
      weekOnly
    >
      {days.length === 0 ? (
        <p className="text-sm text-slate-400">Недостаточно данных.</p>
      ) : (
        <div className="flex flex-col gap-2">
          {days.map((d) => (
            <HBar
              key={d.day}
              label={d.day}
              value={d.avgIntensity}
              max={max}
              annotation={d.avgIntensity.toFixed(1)}
              color={d.avgIntensity >= 0 ? "bg-green-400" : "bg-red-400"}
            />
          ))}
        </div>
      )}
    </CardShell>
  );
}

// --- Card 7 — Next day effect ---

function Card7NextDay({
  range,
  isActive,
}: {
  range: PeriodRange;
  isActive: boolean;
}) {
  const [data, setData] = useState<NextDayEffectDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        const result = await getNextDayEffects(range.from, range.to);
        setData(
          [...result].sort(
            (a, b) => Math.abs(b.nextDayAvgScore) - Math.abs(a.nextDayAvgScore),
          ),
        );
      } catch {
        setError("Не удалось загрузить данные.");
      } finally {
        setLoading(false);
      }
    })();
  }, [shouldLoad, range.from, range.to]);

  const max = Math.max(...data.map((t) => Math.abs(t.nextDayAvgScore)), 1);

  return (
    <CardShell
      badge="Next day effect"
      badgeClass="bg-blue-100 text-blue-700"
      title="Эффект следующего дня"
      explanation='Средний балл на следующий день после событий с этим тегом. "После «аргумент» — следующий день в среднем тяжелее."'
      loading={loading}
      error={error}
    >
      {data.length === 0 ? (
        <p className="text-sm text-slate-400">
          Недостаточно данных для анализа.
        </p>
      ) : (
        <div className="flex flex-col gap-2">
          {data.slice(0, 6).map((t) => (
            <HBar
              key={t.tagName}
              label={t.tagName}
              value={t.nextDayAvgScore}
              max={max}
              annotation={t.nextDayAvgScore.toFixed(1)}
              color={t.nextDayAvgScore >= 0 ? "bg-green-400" : "bg-red-400"}
            />
          ))}
        </div>
      )}
    </CardShell>
  );
}

// --- Card 8 — Tag combos ---

function Card8Combos({
  range,
  isActive,
}: {
  range: PeriodRange;
  isActive: boolean;
}) {
  const [data, setData] = useState<TagComboDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        setData(await getTagCombos(range.from, range.to));
      } catch {
        setError("Не удалось загрузить данные.");
      } finally {
        setLoading(false);
      }
    })();
  }, [shouldLoad, range.from, range.to]);

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
        <p className="text-sm text-slate-400">
          Нет частых комбинаций тегов за период.
        </p>
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
                  <span className="text-slate-400 font-normal ml-1">
                    ×{c.coOccurrences}
                  </span>
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

// --- Card 9 — Tag trend ---

function Card9TagTrend({
  range,
  isActive,
}: {
  range: PeriodRange;
  isActive: boolean;
}) {
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
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        const [triggers, allTags] = await Promise.all([
          getRepeatingTriggers(range.from, range.to),
          getTags(),
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
        const data = await getTagTrend(found.id, range.from, range.to);
        setTrend(data);
        setTagName(top.tagName);
      } catch {
        setError("Не удалось загрузить данные.");
      } finally {
        setLoading(false);
      }
    })();
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
        <p className="text-sm text-slate-400">
          Недостаточно данных для анализа.
        </p>
      ) : (
        <div className="flex flex-col gap-2">
          {tagName && (
            <span className="text-xs font-medium text-slate-700">
              «{tagName}»
            </span>
          )}
          <ResponsiveContainer width="100%" height={160}>
            <BarChart
              data={chartData}
              margin={{ top: 4, right: 4, left: -20, bottom: 0 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
              <XAxis dataKey="week" tick={{ fontSize: 10 }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 10 }} />
              <Tooltip />
              <Bar
                dataKey="count"
                name="Раз в неделю"
                fill="#93c5fd"
                radius={[2, 2, 0, 0]}
              />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </CardShell>
  );
}

// --- Card 10 — Influenceability ---

function Card10Influence({
  range,
  isActive,
}: {
  range: PeriodRange;
  isActive: boolean;
}) {
  const [data, setData] = useState<InfluenceabilitySplitDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);

  useEffect(() => {
    if (isActive) setShouldLoad(true);
  }, [isActive]);
  useEffect(() => {
    if (!shouldLoad) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        setData(await getInfluenceability(range.from, range.to));
      } catch {
        setError("Не удалось загрузить данные.");
      } finally {
        setLoading(false);
      }
    })();
  }, [shouldLoad, range.from, range.to]);

  const total = data ? data.canInfluenceCount + data.cannotInfluenceCount : 0;
  const canPct =
    total > 0 ? Math.round((data!.canInfluenceCount / total) * 100) : 0;
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
              <span className="text-2xl font-bold text-teal-700">
                {data.canInfluenceCount}
              </span>
              <span className="text-slate-500">
                ср. инт. {data.canInfluenceAvgIntensity.toFixed(1)}/10
              </span>
            </div>
            <div className="flex flex-col gap-0.5">
              <span className="font-medium text-slate-600">
                Не могу повлиять
              </span>
              <span className="text-2xl font-bold text-slate-600">
                {data.cannotInfluenceCount}
              </span>
              <span className="text-slate-500">
                ср. инт. {data.cannotInfluenceAvgIntensity.toFixed(1)}/10
              </span>
            </div>
          </div>
        </div>
      ) : (
        !loading &&
        !error && <p className="text-sm text-slate-400">Недостаточно данных.</p>
      )}
    </CardShell>
  );
}

// --- InsightsPage ---

const TOTAL_CARDS = 10;

type CardComponent = (props: {
  range: PeriodRange;
  isActive: boolean;
}) => React.ReactElement | null;

const CARDS: CardComponent[] = [
  Card1Triggers,
  Card2Repeating,
  Card3Balance,
  Card4Trend,
  Card5BlindSpot,
  Card6DayOfWeek,
  Card7NextDay,
  Card8Combos,
  Card9TagTrend,
  Card10Influence,
];

export function InsightsPage() {
  const [period, setPeriod] = useState<Period>(7);
  const [activeIndex, setActiveIndex] = useState(0);

  const range = getPeriodRange(period);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <PeriodSelector
          period={period}
          onChange={(p) => {
            setPeriod(p);
          }}
        />
        <span className="text-xs text-slate-400">
          {activeIndex + 1} / {TOTAL_CARDS}
        </span>
      </div>

      <div>
        {CARDS.map((Card, i) => (
          <div key={i} className={i === activeIndex ? "block" : "hidden"}>
            <Card range={range} isActive={i === activeIndex} />
          </div>
        ))}
      </div>

      <CarouselNav
        total={TOTAL_CARDS}
        activeIndex={activeIndex}
        onPrev={() => setActiveIndex((i) => Math.max(0, i - 1))}
        onNext={() => setActiveIndex((i) => Math.min(TOTAL_CARDS - 1, i + 1))}
      />
    </div>
  );
}
