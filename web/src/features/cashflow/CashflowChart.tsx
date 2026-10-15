import '@mantine/charts/styles.css';
import { CompositeChart } from '@mantine/charts';
import { useConfig } from '../../shared/config';
import { monthLabel, type CashflowMonth } from './types';

export function CashflowChart({ months, currency }: { months: CashflowMonth[]; currency: string }) {
  const { culture } = useConfig();
  const compact = new Intl.NumberFormat(culture, {
    style: 'currency',
    currency,
    notation: 'compact',
    maximumFractionDigits: 1,
  });
  const full = new Intl.NumberFormat(culture, { style: 'currency', currency, maximumFractionDigits: 0 });
  const data = months.map((m) => ({
    month: monthLabel(m.month, culture),
    In: m.inflow,
    Out: -m.outflow,
    Cumulative: m.cumulative,
  }));
  return (
    <CompositeChart
      h={320}
      data={data}
      dataKey="month"
      withLegend
      curveType="linear"
      referenceLines={[{ y: 0, color: 'gray.6' }]}
      valueFormatter={(v) => full.format(v)}
      yAxisProps={{ tickFormatter: (v: number) => compact.format(v) }}
      series={[
        { name: 'In', color: 'teal.6', type: 'bar' },
        { name: 'Out', color: 'red.6', type: 'bar' },
        { name: 'Cumulative', color: 'blue.6', type: 'line' },
      ]}
    />
  );
}
