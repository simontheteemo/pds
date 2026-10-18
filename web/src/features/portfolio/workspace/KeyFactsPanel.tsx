import { Card, SimpleGrid, Text, Title } from '@mantine/core';
import { useConfig } from '../../../shared/config';
import { formatDate, formatMoney } from '../../../shared/format';
import type { OverviewPanelProps } from './overviewPanels';
import { Field } from './Field';

export function KeyFactsPanel({ project: p }: OverviewPanelProps) {
  const config = useConfig();
  const address = [p.site.addressLine, p.site.suburb, p.site.city].filter(Boolean).join(', ');
  const range = (start: string | null, end: string | null) =>
    start || end ? `${formatDate(start, config.culture)} → ${formatDate(end, config.culture)}` : null;
  const legal = [p.site.legalDescription, p.site.titleReference].filter(Boolean).join(' / ');

  return (
    <Card withBorder>
      <Title order={4} mb="sm">
        Key facts
      </Title>
      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }}>
        <Field label="Address">{address}</Field>
        <Field label="Project manager">{p.projectManager}</Field>
        <Field label="Budget">{formatMoney(p.budgetAmount, p.currency, config.culture)}</Field>
        <Field label="Planned start → completion">{range(p.plannedStart, p.plannedCompletion)}</Field>
        <Field label="Actual start → completion">{range(p.actualStart, p.actualCompletion)}</Field>
        <Field label="Land area">
          {p.site.landAreaSqm === null ? null : `${p.site.landAreaSqm.toLocaleString(config.culture)} m²`}
        </Field>
        <Field label="Legal description / title reference">{legal}</Field>
      </SimpleGrid>
      {p.description && (
        <Text mt="md" lineClamp={4} style={{ whiteSpace: 'pre-wrap' }}>
          {p.description}
        </Text>
      )}
    </Card>
  );
}
