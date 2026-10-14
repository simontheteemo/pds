import { Alert, Anchor, Badge, Loader, Stack, Table, Text, Title } from '@mantine/core';
import { Link } from 'react-router';
import { useConfig } from '../../shared/config';
import { formatMoney } from '../../shared/format';
import { useSalesSummaries } from './api';
import { soldPercent, type SalesSummary } from './types';

const numeric = { ta: 'right', style: { fontVariantNumeric: 'tabular-nums' } } as const;

const countKeys = ['totalUnits', 'available', 'reserved', 'conditional', 'unconditional', 'settled', 'withdrawn'] as const;
const valueKeys = ['listValue', 'contractedValue', 'settledValue'] as const;

function sumSummaries(summaries: SalesSummary[]): SalesSummary {
  const total: SalesSummary = {
    totalUnits: 0, available: 0, reserved: 0, conditional: 0, unconditional: 0, settled: 0, withdrawn: 0,
    listValue: 0, contractedValue: 0, settledValue: 0,
    currency: summaries[0]?.currency ?? '',
  };
  for (const s of summaries) {
    for (const key of countKeys) total[key] += s[key];
    for (const key of valueKeys) total[key] += s[key];
  }
  return total;
}

export function SalesPage() {
  const config = useConfig();
  const summaries = useSalesSummaries();
  const data = summaries.data;
  const percent = new Intl.NumberFormat(config.culture, { style: 'percent' });
  const sold = (s: SalesSummary) => {
    const value = soldPercent(s);
    return value === null ? '—' : percent.format(value);
  };

  const cells = (s: SalesSummary) => (
    <>
      <Table.Td {...numeric}>{s.totalUnits}</Table.Td>
      <Table.Td {...numeric}>{s.available}</Table.Td>
      <Table.Td {...numeric}>{s.reserved}</Table.Td>
      <Table.Td {...numeric}>{s.conditional}</Table.Td>
      <Table.Td {...numeric}>{s.unconditional}</Table.Td>
      <Table.Td {...numeric}>{s.settled}</Table.Td>
      <Table.Td {...numeric}>{formatMoney(s.listValue, s.currency, config.culture)}</Table.Td>
      <Table.Td {...numeric}>{formatMoney(s.contractedValue, s.currency, config.culture)}</Table.Td>
      <Table.Td {...numeric}>{formatMoney(s.settledValue, s.currency, config.culture)}</Table.Td>
      <Table.Td {...numeric}>{sold(s)}</Table.Td>
    </>
  );

  return (
    <Stack>
      <Title order={2}>Sales</Title>

      {summaries.error ? (
        <Alert color="red" title="Could not load sales">
          {summaries.error.message}
        </Alert>
      ) : !data ? (
        <Loader />
      ) : data.length === 0 ? (
        <Text c="dimmed">No units recorded on any project yet.</Text>
      ) : (
        <Table.ScrollContainer minWidth={1100}>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Project</Table.Th>
                <Table.Th ta="right">Units</Table.Th>
                <Table.Th ta="right">Available</Table.Th>
                <Table.Th ta="right">Under offer</Table.Th>
                <Table.Th ta="right">Conditional</Table.Th>
                <Table.Th ta="right">Unconditional</Table.Th>
                <Table.Th ta="right">Settled</Table.Th>
                <Table.Th ta="right">List value</Table.Th>
                <Table.Th ta="right">Contracted</Table.Th>
                <Table.Th ta="right">Settled value</Table.Th>
                <Table.Th ta="right">Sold %</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {data.map((row) => (
                <Table.Tr key={row.projectId}>
                  <Table.Td>
                    <Anchor component={Link} to={`/projects/${row.projectId}`}>
                      {row.projectCode} · {row.projectName}
                    </Anchor>
                    {row.projectArchived && (
                      <Badge ml="xs" color="gray" variant="outline">
                        Archived
                      </Badge>
                    )}
                  </Table.Td>
                  {cells(row.summary)}
                </Table.Tr>
              ))}
            </Table.Tbody>
            <Table.Tfoot>
              <Table.Tr fw={700}>
                <Table.Td>Total</Table.Td>
                {cells(sumSummaries(data.map((row) => row.summary)))}
              </Table.Tr>
            </Table.Tfoot>
          </Table>
        </Table.ScrollContainer>
      )}
    </Stack>
  );
}
