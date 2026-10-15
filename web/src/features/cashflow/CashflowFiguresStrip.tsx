import { Group, Stack, Text } from '@mantine/core';
import { useConfig } from '../../shared/config';
import { formatMoney } from '../../shared/format';
import { monthLabel, type CashflowTotals } from './types';

function Figure({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <Stack gap={0}>
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      <Text fw={600} size="lg" c={color}>
        {value}
      </Text>
    </Stack>
  );
}

export function CashflowFiguresStrip({ totals, currency }: { totals: CashflowTotals; currency: string }) {
  const { culture } = useConfig();
  const money = (n: number) => formatMoney(n, currency, culture);
  const peak =
    totals.peakFunding === null || totals.peakFundingMonth === null
      ? 'None'
      : `${money(totals.peakFunding)} in ${monthLabel(totals.peakFundingMonth, culture)}`;
  return (
    <Group gap="xl">
      <Figure label="Total out" value={money(totals.outflow)} />
      <Figure label="Total in" value={money(totals.inflow)} />
      <Figure label="Net" value={money(totals.net)} color={totals.net < 0 ? 'red' : undefined} />
      <Figure label="Peak funding" value={peak} />
      {totals.unscheduledCost > 0 && <Figure label="Unscheduled cost" value={money(totals.unscheduledCost)} />}
      {totals.unscheduledSales > 0 && <Figure label="Unscheduled sales" value={money(totals.unscheduledSales)} />}
    </Group>
  );
}
