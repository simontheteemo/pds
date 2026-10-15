import { Table } from '@mantine/core';
import { useConfig } from '../../shared/config';
import { formatMoney } from '../../shared/format';
import { monthLabel, type CashflowMonth, type CashflowTotals } from './types';

export function CashflowTable({
  months,
  totals,
  asOf,
  currency,
}: {
  months: CashflowMonth[];
  totals: CashflowTotals;
  asOf: string;
  currency: string;
}) {
  const { culture } = useConfig();
  const money = (n: number) => formatMoney(n, currency, culture);
  const currentMonth = asOf.slice(0, 7);
  return (
    <Table.ScrollContainer minWidth={720}>
      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Month</Table.Th>
            <Table.Th ta="right">Cost (actual)</Table.Th>
            <Table.Th ta="right">Cost (forecast)</Table.Th>
            <Table.Th ta="right">Sales (settled)</Table.Th>
            <Table.Th ta="right">Sales (forecast)</Table.Th>
            <Table.Th ta="right">Net</Table.Th>
            <Table.Th ta="right">Cumulative</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {months.map((m) => (
            <Table.Tr key={m.month} bg={m.month === currentMonth ? 'var(--mantine-color-blue-light)' : undefined}>
              <Table.Td>{monthLabel(m.month, culture)}</Table.Td>
              <Table.Td ta="right">{money(m.costActual)}</Table.Td>
              <Table.Td ta="right">{money(m.costForecast)}</Table.Td>
              <Table.Td ta="right">{money(m.salesSettled)}</Table.Td>
              <Table.Td ta="right">{money(m.salesForecast)}</Table.Td>
              <Table.Td ta="right">{money(m.net)}</Table.Td>
              <Table.Td ta="right" c={m.cumulative < 0 ? 'red' : undefined}>
                {money(m.cumulative)}
              </Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
        <Table.Tfoot>
          <Table.Tr fw={600}>
            <Table.Td>Total</Table.Td>
            <Table.Td ta="right">{money(totals.costActual)}</Table.Td>
            <Table.Td ta="right">{money(totals.costForecast)}</Table.Td>
            <Table.Td ta="right">{money(totals.salesSettled)}</Table.Td>
            <Table.Td ta="right">{money(totals.salesForecast)}</Table.Td>
            <Table.Td ta="right">{money(totals.net)}</Table.Td>
            <Table.Td />
          </Table.Tr>
        </Table.Tfoot>
      </Table>
    </Table.ScrollContainer>
  );
}
