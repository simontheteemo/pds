import { Group, Stack, Text } from '@mantine/core';
import { useConfig } from '../../shared/config';
import { formatMoney } from '../../shared/format';
import type { CostFigures } from './types';

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

export function CostFiguresStrip({
  figures,
  currency,
  showFtc = false,
}: {
  figures: CostFigures;
  currency: string;
  showFtc?: boolean;
}) {
  const { culture } = useConfig();
  const money = (n: number) => formatMoney(n, currency, culture);
  const varianceColor = figures.variance < 0 ? 'red' : figures.variance > 0 ? 'green' : undefined;
  return (
    <Group gap="xl">
      <Figure label="Budget" value={money(figures.budget)} />
      <Figure label="Committed" value={money(figures.committed)} />
      <Figure label="Actual" value={money(figures.actual)} />
      {showFtc && <Figure label="Forecast to complete" value={money(figures.forecastToComplete)} />}
      <Figure label="Forecast final cost" value={money(figures.forecastFinalCost)} />
      <Figure label="Variance" value={money(figures.variance)} color={varianceColor} />
    </Group>
  );
}
