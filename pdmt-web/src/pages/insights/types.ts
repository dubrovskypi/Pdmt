export type Period = 7 | 14 | 30;

export interface PeriodRange {
  from: string;
  to: string;
}

export type CardComponent = (props: {
  range: PeriodRange;
  isActive: boolean;
}) => React.ReactElement | null;

export const PERIOD_LABELS: Record<Period, string> = {
  7: "7 дней",
  14: "14 дней",
  30: "30 дней",
};

export function getPeriodRange(period: Period): PeriodRange {
  const to = new Date();
  const from = new Date(to.getTime() - period * 24 * 60 * 60 * 1000);
  return {
    from: from.toISOString(),
    to: to.toISOString(),
  };
}
