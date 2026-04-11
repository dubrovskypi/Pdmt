import { useState } from "react";
import { Button } from "@/components/ui/button";
import { getPeriodRange, PERIOD_LABELS, type Period, type CardComponent } from "./insights/types";
import { Card1Triggers } from "./insights/Card1Triggers";
import { Card2Repeating } from "./insights/Card2Repeating";
import { Card3Balance } from "./insights/Card3Balance";
import { Card4Trend } from "./insights/Card4Trend";
import { Card5BlindSpot } from "./insights/Card5BlindSpot";
import { Card6DayOfWeek } from "./insights/Card6DayOfWeek";
import { Card7NextDay } from "./insights/Card7NextDay";
import { Card8Combos } from "./insights/Card8Combos";
import { Card9TagTrend } from "./insights/Card9TagTrend";
import { Card10Influence } from "./insights/Card10Influence";

// --- PeriodSelector ---

function PeriodSelector({ period, onChange }: { period: Period; onChange: (p: Period) => void }) {
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

// --- InsightsPage ---

const TOTAL_CARDS = 10;

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
