import { Badge, Button, Group, Table, Text } from '@mantine/core';
import { useConfig } from '../../shared/config';
import { formatDate, formatMoney } from '../../shared/format';
import { InvoiceFiles, invoiceLinkLabel, useInvoiceAttachments } from './InvoiceAttachments';
import {
  actualCostTypeLabel,
  invoiceStatusColor,
  invoiceStatusLabel,
  type CostReportActual,
  type InvoiceStatus,
} from './types';

type Entry = CostReportActual['entry'];

type Props = {
  projectId: string;
  currency: string;
  rows: CostReportActual[];
  /** Edit is offered (and files can be attached): the user can write and the project isn't archived. */
  writable: boolean;
  deletable: boolean;
  onEdit: (entry: Entry) => void;
  onDelete: (entry: Entry) => void;
};

const sum = (ns: number[]) => Math.round(ns.reduce((a, b) => a + b, 0) * 100) / 100;

export function StatusBadge({ status }: { status: string }) {
  const known = status in invoiceStatusLabel ? (status as InvoiceStatus) : null;
  return (
    <Badge color={known ? invoiceStatusColor[known] : 'gray'} variant="light">
      {known ? invoiceStatusLabel[known] : status}
    </Badge>
  );
}

/** The Invoices & claims table of a project's cost report. */
export function InvoicesTable({ projectId, currency, rows, writable, deletable, onEdit, onDelete }: Props) {
  const { culture } = useConfig();
  const attachments = useInvoiceAttachments([projectId]);
  const showActions = writable || deletable;
  const money = (n: number | null) => formatMoney(n, currency, culture);
  const moneyCell = (n: number | null, th = false) => {
    const Cell = th ? Table.Th : Table.Td;
    return (
      <Cell ta="right" c={n !== null && n < 0 ? 'red' : undefined}>
        {money(n)}
      </Cell>
    );
  };
  const typeLabel = (type: string): string => (actualCostTypeLabel as Record<string, string>)[type] ?? type;
  const totalAmount = sum(rows.map((r) => r.entry.amount));
  const totalGst = sum(rows.map((r) => r.entry.gstAmount ?? 0));

  return (
    <Table.ScrollContainer minWidth={1100}>
      <Table highlightOnHover>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Date</Table.Th>
            <Table.Th>Contractor / payee</Table.Th>
            <Table.Th>No.</Table.Th>
            <Table.Th>Line</Table.Th>
            <Table.Th>Contract</Table.Th>
            <Table.Th>Due</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th ta="right">Amount</Table.Th>
            <Table.Th ta="right">GST</Table.Th>
            <Table.Th>Files</Table.Th>
            {showActions && <Table.Th />}
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {rows.map((row) => {
            const e = row.entry;
            const who = row.contractorName ?? e.payee;
            return (
              <Table.Tr key={e.id}>
                <Table.Td>{formatDate(e.date, culture)}</Table.Td>
                <Table.Td>
                  <Text size="sm">{who ?? '—'}</Text>
                  <Text size="xs" c="dimmed" lineClamp={1}>
                    {e.description}
                  </Text>
                </Table.Td>
                <Table.Td>
                  <Text size="sm">{e.reference ?? '—'}</Text>
                  <Text size="xs" c="dimmed">
                    {typeLabel(e.type)}
                  </Text>
                </Table.Td>
                <Table.Td>{row.budgetLineName}</Table.Td>
                <Table.Td>{row.contractTitle ?? '—'}</Table.Td>
                <Table.Td>{formatDate(e.dueDate, culture)}</Table.Td>
                <Table.Td>
                  <StatusBadge status={e.status} />
                  {e.status === 'Paid' && e.paidDate && (
                    <Text size="xs" c="dimmed">
                      {formatDate(e.paidDate, culture)}
                    </Text>
                  )}
                </Table.Td>
                {moneyCell(e.amount)}
                {moneyCell(e.gstAmount)}
                <Table.Td>
                  <InvoiceFiles
                    projectId={projectId}
                    docs={attachments.get(e.id) ?? []}
                    canAttach={writable}
                    invoiceLabel={invoiceLinkLabel(who, e.reference ?? e.description)}
                    invoiceId={e.id}
                  />
                </Table.Td>
                {showActions && (
                  <Table.Td>
                    <Group gap="xs" wrap="nowrap" justify="flex-end">
                      {writable && (
                        <Button size="xs" variant="light" onClick={() => onEdit(e)}>
                          Edit
                        </Button>
                      )}
                      {deletable && (
                        <Button size="xs" variant="light" color="red" onClick={() => onDelete(e)}>
                          Delete
                        </Button>
                      )}
                    </Group>
                  </Table.Td>
                )}
              </Table.Tr>
            );
          })}
        </Table.Tbody>
        <Table.Tfoot>
          <Table.Tr>
            <Table.Th colSpan={7}>Total</Table.Th>
            {moneyCell(totalAmount, true)}
            {moneyCell(totalGst, true)}
            <Table.Th />
            {showActions && <Table.Th />}
          </Table.Tr>
        </Table.Tfoot>
      </Table>
    </Table.ScrollContainer>
  );
}
