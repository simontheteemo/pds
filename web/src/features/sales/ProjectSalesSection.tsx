import { Alert, Button, Card, Group, Loader, Stack, Table, Text, Title } from '@mantine/core';
import { useState } from 'react';
import { useConfig } from '../../shared/config';
import { formatDate, formatMoney } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { useProjectSales } from './api';
import { SalesSummaryStrip } from './SalesSummaryStrip';
import { SaleStatusBadge } from './SaleStatusBadge';
import { unitTypeLabel, type UnitDetails } from './types';
import { SaleFormModal } from './SaleFormModal';
import { UnitFormModal } from './UnitFormModal';

const DASH = '—';

const rooms = (u: UnitDetails) => [u.bedrooms, u.bathrooms, u.carParks].map((n) => (n === null ? DASH : String(n))).join(' · ');

/** Spec §5: the one date that matters for the unit's current status. */
function keyDate(u: UnitDetails, culture: string): string {
  switch (u.status) {
    case 'Conditional':
      return u.agreementDate ? `S&P ${formatDate(u.agreementDate, culture)}` : DASH;
    case 'Unconditional':
      if (u.settlementDate) return `Settles ${formatDate(u.settlementDate, culture)}`;
      return u.unconditionalDate ? `Unconditional ${formatDate(u.unconditionalDate, culture)}` : DASH;
    case 'Settled':
      return u.settlementDate ? `Settled ${formatDate(u.settlementDate, culture)}` : DASH;
    default:
      return DASH;
  }
}

export function ProjectSalesSection({ projectId, projectArchived }: { projectId: string; projectArchived: boolean }) {
  const config = useConfig();
  const { canWrite } = usePermissions();
  const [editing, setEditing] = useState<UnitDetails | 'new' | undefined>(undefined);
  const [sale, setSale] = useState<UnitDetails | undefined>(undefined);
  const sales = useProjectSales(projectId);
  const areaFormat = new Intl.NumberFormat(config.culture, { maximumFractionDigits: 2 });
  const area = (v: number | null) => (v === null ? DASH : areaFormat.format(v));

  return (
    <Card withBorder>
      <Group justify="space-between" mb="sm">
        <Title order={4}>Sales</Title>
        {canWrite && !projectArchived && <Button onClick={() => setEditing('new')}>Add unit</Button>}
      </Group>

      {sales.error ? (
        <Alert color="red" title="Could not load sales">{sales.error.message}</Alert>
      ) : !sales.data ? (
        <Loader size="sm" />
      ) : sales.data.units.length === 0 ? (
        <Text c="dimmed">No units recorded.</Text>
      ) : (
        <Stack gap="md">
          <SalesSummaryStrip summary={sales.data.summary} />
          <Table.ScrollContainer minWidth={960}>
            <Table highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Unit</Table.Th>
                  <Table.Th>Type</Table.Th>
                  <Table.Th>Bed · Bath · Car</Table.Th>
                  <Table.Th>Floor m²</Table.Th>
                  <Table.Th>Land m²</Table.Th>
                  <Table.Th>List price</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Buyer</Table.Th>
                  <Table.Th>Sale price</Table.Th>
                  <Table.Th>Key date</Table.Th>
                  {canWrite && <Table.Th>Actions</Table.Th>}
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {sales.data.units.map((unit) => (
                  <Table.Tr key={unit.id} opacity={unit.status === 'Withdrawn' ? 0.6 : 1}>
                    <Table.Td><Text fw={700}>{unit.identifier}</Text></Table.Td>
                    <Table.Td>{unitTypeLabel[unit.type]}</Table.Td>
                    <Table.Td>{rooms(unit)}</Table.Td>
                    <Table.Td>{area(unit.floorAreaSqm)}</Table.Td>
                    <Table.Td>{area(unit.landAreaSqm)}</Table.Td>
                    <Table.Td>{formatMoney(unit.listPriceAmount, unit.currency, config.culture)}</Table.Td>
                    <Table.Td><SaleStatusBadge status={unit.status} /></Table.Td>
                    <Table.Td>{unit.buyerName ?? DASH}</Table.Td>
                    <Table.Td>{formatMoney(unit.salePriceAmount, unit.currency, config.culture)}</Table.Td>
                    <Table.Td>{keyDate(unit, config.culture)}</Table.Td>
                    {canWrite && (
                      <Table.Td>
                        <Group gap="xs" justify="flex-end" wrap="nowrap">
                          <Button size="xs" variant="light" onClick={() => setEditing(unit)}>Edit</Button>
                          <Button size="xs" variant="light" onClick={() => setSale(unit)}>Sale</Button>
                        </Group>
                      </Table.Td>
                    )}
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Stack>
      )}

      <UnitFormModal projectId={projectId} unit={editing} onClose={() => setEditing(undefined)} />
      <SaleFormModal projectId={projectId} unit={sale} onClose={() => setSale(undefined)} />
    </Card>
  );
}
