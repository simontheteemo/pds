import { Button, Group, Modal, Select, Stack, Table, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { formatDate, formatMoney } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { CostEntryFormModal, type CostEntry } from './CostEntryFormModal';
import type { CostEntryKind } from './costEntryFormModel';
import {
  actualCostTypeLabel,
  commitmentTypeLabel,
  type CostReportActual,
  type CostReportCommitment,
  type ProjectCostReport,
} from './types';

type Row = CostReportCommitment | CostReportActual;

type Props = {
  kind: CostEntryKind;
  report: ProjectCostReport;
  rows: Row[];
  deleteEntry: {
    isPending: boolean;
    mutate: (
      vars: { entryId: string; version: number },
      opts: { onSuccess: () => void; onError: (error: Error) => void },
    ) => void;
  };
};

/** Shared implementation of the Commitments and Actuals tabs. */
export function CostEntriesTab({ kind, report, rows, deleteEntry }: Props) {
  const { culture } = useConfig();
  const { canWrite, canAdminister } = usePermissions();
  const [lineFilter, setLineFilter] = useState<string | null>(null);
  const [editing, setEditing] = useState<CostEntry | 'new' | undefined>(undefined);
  const [deleting, setDeleting] = useState<CostEntry | undefined>(undefined);
  const isActual = kind === 'actual';
  const noun = isActual ? 'actual' : 'commitment';
  const writable = canWrite && !report.projectArchived;
  const deletable = canAdminister && !report.projectArchived;
  const showActions = writable || deletable;
  const money = (n: number) => formatMoney(n, report.currency, culture);
  const labels: Record<string, string> = isActual ? actualCostTypeLabel : commitmentTypeLabel;
  const typeLabel = (type: string): string => labels[type] ?? type;

  const filtered = lineFilter ? rows.filter((r) => r.entry.budgetLineId === lineFilter) : rows;
  const total = Math.round(filtered.reduce((sum, r) => sum + r.entry.amount, 0) * 100) / 100;
  const colCount = (isActual ? 8 : 7) + (showActions ? 1 : 0);
  const amountCell = (n: number, th = false) => {
    const Cell = th ? Table.Th : Table.Td;
    return (
      <Cell ta="right" c={n < 0 ? 'red' : undefined}>
        {money(n)}
      </Cell>
    );
  };

  const confirmDelete = () => {
    if (!deleting) return;
    deleteEntry.mutate(
      { entryId: deleting.id, version: deleting.version },
      {
        onSuccess: () => {
          notifications.show({ color: 'green', message: `${isActual ? 'Actual' : 'Commitment'} deleted` });
          setDeleting(undefined);
        },
        onError: (error) => {
          const conflict = error instanceof ApiError && error.status === 409;
          notifications.show({
            color: conflict ? 'yellow' : 'red',
            title: conflict ? 'This item was changed by someone else' : `Could not delete the ${noun}`,
            message:
              error instanceof ApiError && error.status === 400 && error.problem?.errors
                ? Object.values(error.problem.errors).flat().join(' ')
                : conflict
                  ? 'The cost report now shows the latest version. Open it again to make your changes.'
                  : error.message,
          });
          setDeleting(undefined);
        },
      },
    );
  };

  return (
    <Stack>
      <Group justify="space-between" align="flex-end">
        <Select
          label="Budget line"
          placeholder="All lines"
          clearable
          value={lineFilter}
          onChange={setLineFilter}
          data={report.lines.map(({ line }) => ({ value: line.id, label: line.name }))}
        />
        {writable && <Button onClick={() => setEditing('new')}>{isActual ? 'Add actual' : 'Add commitment'}</Button>}
      </Group>

      {filtered.length === 0 ? (
        <Text c="dimmed">{isActual ? 'No actuals recorded.' : 'No commitments recorded.'}</Text>
      ) : (
        <Table.ScrollContainer minWidth={900}>
          <Table highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Date</Table.Th>
                <Table.Th>Line</Table.Th>
                <Table.Th>Type</Table.Th>
                {isActual && <Table.Th>Payee</Table.Th>}
                <Table.Th>Description</Table.Th>
                <Table.Th>Reference</Table.Th>
                <Table.Th>Contract</Table.Th>
                <Table.Th ta="right">Amount</Table.Th>
                {showActions && <Table.Th />}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {filtered.map((row) => {
                const e = row.entry;
                return (
                  <Table.Tr key={e.id}>
                    <Table.Td>{formatDate(e.date, culture)}</Table.Td>
                    <Table.Td>{row.budgetLineName}</Table.Td>
                    <Table.Td>{typeLabel(e.type)}</Table.Td>
                    {isActual && <Table.Td>{'payee' in e && e.payee ? e.payee : '—'}</Table.Td>}
                    <Table.Td>{e.description}</Table.Td>
                    <Table.Td>{e.reference ?? '—'}</Table.Td>
                    <Table.Td>
                      {row.contractTitle ? `${row.contractorName ?? ''} — ${row.contractTitle}` : '—'}
                    </Table.Td>
                    {amountCell(e.amount)}
                    {showActions && (
                      <Table.Td>
                        <Group gap="xs" wrap="nowrap" justify="flex-end">
                          {writable && (
                            <Button size="xs" variant="light" onClick={() => setEditing(e)}>
                              Edit
                            </Button>
                          )}
                          {deletable && (
                            <Button size="xs" variant="light" color="red" onClick={() => setDeleting(e)}>
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
                <Table.Th colSpan={colCount - 1 - (showActions ? 1 : 0)}>Total</Table.Th>
                {amountCell(total, true)}
                {showActions && <Table.Th />}
              </Table.Tr>
            </Table.Tfoot>
          </Table>
        </Table.ScrollContainer>
      )}

      <CostEntryFormModal
        projectId={report.projectId}
        kind={kind}
        lines={report.lines}
        contracts={report.contracts}
        entry={editing}
        defaultLineId={lineFilter}
        onClose={() => setEditing(undefined)}
      />

      <Modal opened={deleting !== undefined} onClose={() => setDeleting(undefined)} title={`Delete ${noun}`}>
        <Stack>
          <Text>
            Delete the {noun} &ldquo;{deleting?.description}&rdquo;? This cannot be undone.
          </Text>
          <Group>
            <Button color="red" loading={deleteEntry.isPending} onClick={confirmDelete}>
              Delete
            </Button>
            <Button variant="default" onClick={() => setDeleting(undefined)}>
              Cancel
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}
