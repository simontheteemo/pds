import { Button, Group, Modal, Select, Stack, Table, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { useConfig } from '../../shared/config';
import { formatDate, formatMoney, joinLabel } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { CostEntryFormModal, type CostEntry } from './CostEntryFormModal';
import type { CostEntryKind } from './costEntryFormModel';
import { showDeleteError } from './deleteErrorToast';
import { InvoicesTable } from './InvoicesTable';
import {
  commitmentTypeLabel,
  type CostReportActual,
  type CostReportCommitment,
  type ProjectCostReport,
} from './types';

type Row = CostReportCommitment | CostReportActual;

const isActualRow = (row: Row): row is CostReportActual => 'status' in row.entry;

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

/**
 * Shared implementation of the Commitments and Invoices & claims tabs: the budget-line filter, the add / edit form and
 * the delete confirmation. Actuals render through `InvoicesTable`.
 */
export function CostEntriesTab({ kind, report, rows, deleteEntry }: Props) {
  const { culture } = useConfig();
  const { canWrite, canAdminister } = usePermissions();
  const [lineFilter, setLineFilter] = useState<string | null>(null);
  // A filter on a line that no longer exists (e.g. deleted) is treated as "all lines".
  const effectiveFilter = lineFilter && report.lines.some(({ line }) => line.id === lineFilter) ? lineFilter : null;
  if (lineFilter !== effectiveFilter) setLineFilter(effectiveFilter);
  const [editing, setEditing] = useState<CostEntry | 'new' | undefined>(undefined);
  const [deleting, setDeleting] = useState<CostEntry | undefined>(undefined);
  const isActual = kind === 'actual';
  const noun = isActual ? 'invoice' : 'commitment';
  const writable = canWrite && !report.projectArchived;
  const deletable = canAdminister && !report.projectArchived;
  const showActions = writable || deletable;
  const money = (n: number) => formatMoney(n, report.currency, culture);
  const typeLabel = (type: string): string => (commitmentTypeLabel as Record<string, string>)[type] ?? type;

  const filtered = effectiveFilter ? rows.filter((r) => r.entry.budgetLineId === effectiveFilter) : rows;
  const total = Math.round(filtered.reduce((sum, r) => sum + r.entry.amount, 0) * 100) / 100;
  const colCount = 7 + (showActions ? 1 : 0);
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
          notifications.show({ color: 'green', message: `${isActual ? 'Invoice' : 'Commitment'} deleted` });
          setDeleting(undefined);
        },
        onError: (error) => {
          showDeleteError(error, noun);
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
          value={effectiveFilter}
          onChange={setLineFilter}
          data={report.lines.map(({ line }) => ({ value: line.id, label: line.name }))}
        />
        {writable && <Button onClick={() => setEditing('new')}>{isActual ? 'Add invoice' : 'Add commitment'}</Button>}
      </Group>

      {filtered.length === 0 ? (
        rows.length > 0 ? (
          <Group gap="xs">
            <Text c="dimmed">No {isActual ? 'invoices or claims' : 'commitments'} on this budget line.</Text>
            <Button size="compact-sm" variant="subtle" onClick={() => setLineFilter(null)}>
              Show all lines
            </Button>
          </Group>
        ) : (
          <Text c="dimmed">{isActual ? 'No invoices or claims recorded.' : 'No commitments recorded.'}</Text>
        )
      ) : isActual ? (
        <InvoicesTable
          projectId={report.projectId}
          currency={report.currency}
          rows={filtered.filter(isActualRow)}
          writable={writable}
          deletable={deletable}
          onEdit={setEditing}
          onDelete={setDeleting}
        />
      ) : (
        <Table.ScrollContainer minWidth={900}>
          <Table highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Date</Table.Th>
                <Table.Th>Line</Table.Th>
                <Table.Th>Type</Table.Th>
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
                    <Table.Td>{e.description}</Table.Td>
                    <Table.Td>{e.reference ?? '—'}</Table.Td>
                    <Table.Td>
                      {joinLabel(row.contractorName, row.contractTitle) || '—'}
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
        defaultLineId={effectiveFilter}
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
