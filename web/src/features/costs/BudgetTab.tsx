import { Badge, Button, Group, Modal, Stack, Table, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { formatMoney } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { useAddStandardLines, useDeleteBudgetLine } from './api';
import { BudgetLineFormModal } from './BudgetLineFormModal';
import type { CostFigures, CostReportLine, ProjectCostReport } from './types';

export function BudgetTab({ report }: { report: ProjectCostReport }) {
  const { culture } = useConfig();
  const { canWrite, canAdminister } = usePermissions();
  const projectId = report.projectId;
  const [editing, setEditing] = useState<CostReportLine['line'] | 'new' | undefined>(undefined);
  const [deleting, setDeleting] = useState<CostReportLine['line'] | undefined>(undefined);
  const addStandard = useAddStandardLines(projectId);
  const deleteLine = useDeleteBudgetLine(projectId);
  const writable = canWrite && !report.projectArchived;
  const deletable = canAdminister && !report.projectArchived;
  const showActions = writable || deletable;
  const money = (n: number) => formatMoney(n, report.currency, culture);

  const onAddStandard = () =>
    addStandard.mutate(undefined, {
      onSuccess: (added) =>
        notifications.show({
          color: 'green',
          message: added.length > 0 ? `Added ${added.length} lines` : 'All standard lines already exist',
        }),
      onError: (error) =>
        notifications.show({
          color: 'red',
          title: 'Could not add the standard lines',
          message: error instanceof Error ? error.message : 'Unexpected error',
        }),
    });

  const confirmDelete = () => {
    if (!deleting) return;
    deleteLine.mutate(
      { lineId: deleting.id, version: deleting.version },
      {
        onSuccess: () => {
          notifications.show({ color: 'green', message: 'Budget line deleted' });
          setDeleting(undefined);
        },
        onError: (error) => {
          const conflict = error instanceof ApiError && error.status === 409;
          notifications.show({
            color: conflict ? 'yellow' : 'red',
            title: conflict ? 'This item was changed by someone else' : 'Could not delete the budget line',
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

  const numCell = (n: number) => <Table.Td ta="right">{money(n)}</Table.Td>;
  const varianceColor = (f: CostFigures) => (f.variance < 0 ? 'red' : f.variance > 0 ? 'green' : undefined);

  return (
    <Stack>
      {writable && (
        <Group>
          <Button onClick={() => setEditing('new')}>Add line</Button>
          <Button variant="default" loading={addStandard.isPending} onClick={onAddStandard}>
            Add standard lines
          </Button>
        </Group>
      )}

      {report.lines.length === 0 ? (
        <Text c="dimmed">No budget lines yet. Add the standard lines to get started.</Text>
      ) : (
        <Table.ScrollContainer minWidth={900}>
          <Table highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Line</Table.Th>
                <Table.Th ta="right">Budget</Table.Th>
                <Table.Th ta="right">Committed</Table.Th>
                <Table.Th ta="right">Actual</Table.Th>
                <Table.Th ta="right">Forecast final cost</Table.Th>
                <Table.Th ta="right">Forecast to complete</Table.Th>
                <Table.Th ta="right">Variance</Table.Th>
                {showActions && <Table.Th />}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {report.lines.map(({ line, figures }) => (
                <Table.Tr key={line.id}>
                  <Table.Td>
                    <Group gap="xs" wrap="nowrap">
                      <span>{line.name}</span>
                      {line.forecastRemainingAmount != null && (
                        <Badge size="xs" variant="light">
                          manual
                        </Badge>
                      )}
                    </Group>
                  </Table.Td>
                  {numCell(figures.budget)}
                  {numCell(figures.committed)}
                  {numCell(figures.actual)}
                  {numCell(figures.forecastFinalCost)}
                  {numCell(figures.forecastToComplete)}
                  <Table.Td ta="right" c={varianceColor(figures)}>
                    {money(figures.variance)}
                  </Table.Td>
                  {showActions && (
                    <Table.Td>
                      <Group gap="xs" wrap="nowrap" justify="flex-end">
                        {writable && (
                          <Button size="xs" variant="light" onClick={() => setEditing(line)}>
                            Edit
                          </Button>
                        )}
                        {deletable && (
                          <Button size="xs" variant="light" color="red" onClick={() => setDeleting(line)}>
                            Delete
                          </Button>
                        )}
                      </Group>
                    </Table.Td>
                  )}
                </Table.Tr>
              ))}
            </Table.Tbody>
            <Table.Tfoot>
              <Table.Tr>
                <Table.Th>Total</Table.Th>
                <Table.Th ta="right">{money(report.totals.budget)}</Table.Th>
                <Table.Th ta="right">{money(report.totals.committed)}</Table.Th>
                <Table.Th ta="right">{money(report.totals.actual)}</Table.Th>
                <Table.Th ta="right">{money(report.totals.forecastFinalCost)}</Table.Th>
                <Table.Th ta="right">{money(report.totals.forecastToComplete)}</Table.Th>
                <Table.Th ta="right" c={varianceColor(report.totals)}>
                  {money(report.totals.variance)}
                </Table.Th>
                {showActions && <Table.Th />}
              </Table.Tr>
            </Table.Tfoot>
          </Table>
        </Table.ScrollContainer>
      )}

      <BudgetLineFormModal projectId={projectId} lines={report.lines} line={editing} onClose={() => setEditing(undefined)} />

      <Modal opened={deleting !== undefined} onClose={() => setDeleting(undefined)} title="Delete budget line">
        <Stack>
          <Text>Delete the budget line &ldquo;{deleting?.name}&rdquo;? This cannot be undone.</Text>
          <Group>
            <Button color="red" loading={deleteLine.isPending} onClick={confirmDelete}>
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
