import { Badge, Group, SimpleGrid, Stack, Text } from '@mantine/core';
import { useConfig } from '../../shared/config';
import { formatMoney } from '../../shared/format';
import { saleStatusColor, saleStatusLabel, saleStatuses, soldPercent, type SalesSummary, type SaleStatus } from './types';

const countFor = (summary: SalesSummary, status: SaleStatus): number => {
  switch (status) {
    case 'Available':
      return summary.available;
    case 'Reserved':
      return summary.reserved;
    case 'Conditional':
      return summary.conditional;
    case 'Unconditional':
      return summary.unconditional;
    case 'Settled':
      return summary.settled;
    case 'Withdrawn':
      return summary.withdrawn;
  }
};

function Figure({ label, value }: { label: string; value: string }) {
  return (
    <Stack gap={0}>
      <Text size="sm" c="dimmed">
        {label}
      </Text>
      <Text fw={600} style={{ fontVariantNumeric: 'tabular-nums' }}>
        {value}
      </Text>
    </Stack>
  );
}

export function SalesSummaryStrip({ summary }: { summary: SalesSummary }) {
  const config = useConfig();
  const money = (amount: number) => formatMoney(amount, summary.currency, config.culture);
  const sold = soldPercent(summary);

  return (
    <Stack gap="sm">
      <Group gap="xs">
        {saleStatuses.map((status) => {
          const count = countFor(summary, status);
          return count > 0 ? (
            <Badge key={status} color={saleStatusColor[status]} variant="light">
              {saleStatusLabel[status]} {count}
            </Badge>
          ) : null;
        })}
        <Text size="sm" c="dimmed">
          {summary.totalUnits === 1 ? '1 unit' : `${summary.totalUnits} units`}
        </Text>
      </Group>
      <SimpleGrid cols={{ base: 2, md: 4 }}>
        <Figure label="List value" value={money(summary.listValue)} />
        <Figure label="Contracted value" value={money(summary.contractedValue)} />
        <Figure label="Settled value" value={money(summary.settledValue)} />
        <Figure
          label="Sold"
          value={sold === null ? '—' : new Intl.NumberFormat(config.culture, { style: 'percent', maximumFractionDigits: 1 }).format(sold)}
        />
      </SimpleGrid>
    </Stack>
  );
}
