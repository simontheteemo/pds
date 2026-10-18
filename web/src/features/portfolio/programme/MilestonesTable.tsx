import { Button, Group, Modal, Table, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ApiError } from '../../../shared/api/client';
import { useConfig } from '../../../shared/config';
import { formatDate } from '../../../shared/format';
import { milestonesKey, useDeleteMilestone, useUpdateMilestone } from './api';
import { MilestoneStatusBadge } from './MilestoneStatusBadge';
import { milestoneCategoryLabel, type MilestoneDetails } from './types';

type Props = {
  projectId: string;
  milestones: MilestoneDetails[];
  /** Server's "today" (yyyy-MM-dd), used as the actual date when marking achieved. */
  today: string;
  canWrite: boolean;
  canAdminister: boolean;
  onEdit: (milestone: MilestoneDetails) => void;
};

function showMilestoneError(error: unknown, title: string) {
  const conflict = error instanceof ApiError && error.status === 409;
  notifications.show({
    color: conflict ? 'yellow' : 'red',
    title: conflict ? 'This milestone was changed by someone else' : title,
    message: conflict
      ? 'The programme now shows the latest version. Try again.'
      : error instanceof Error
        ? error.message
        : 'Unexpected error',
  });
}

export function MilestonesTable({ projectId, milestones, today, canWrite, canAdminister, onEdit }: Props) {
  const config = useConfig();
  const queryClient = useQueryClient();
  const update = useUpdateMilestone(projectId);
  const remove = useDeleteMilestone(projectId);
  const [achievingId, setAchievingId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<MilestoneDetails | null>(null);

  const markAchieved = async (m: MilestoneDetails) => {
    setAchievingId(m.id);
    try {
      await update.mutateAsync({
        milestoneId: m.id,
        body: {
          name: m.name,
          category: m.category,
          plannedDate: m.plannedDate,
          forecastDate: m.forecastDate,
          actualDate: today,
          owner: m.owner,
          notes: m.notes,
          sortOrder: m.sortOrder,
          version: m.version,
        },
      });
      notifications.show({ color: 'green', message: 'Marked achieved' });
    } catch (error) {
      showMilestoneError(error, 'Could not mark the milestone achieved');
      if (error instanceof ApiError && error.status === 409) {
        void queryClient.invalidateQueries({ queryKey: milestonesKey(projectId) });
      }
    } finally {
      setAchievingId(null);
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    const target = deleting;
    try {
      await remove.mutateAsync({ milestoneId: target.id, version: target.version });
      notifications.show({ color: 'green', message: 'Milestone deleted' });
    } catch (error) {
      showMilestoneError(error, 'Could not delete the milestone');
    } finally {
      setDeleting(null);
    }
  };

  const showActions = canWrite || canAdminister;

  return (
    <>
      <Table.ScrollContainer minWidth={820}>
        <Table verticalSpacing="xs">
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Milestone</Table.Th>
              <Table.Th>Planned</Table.Th>
              <Table.Th>Forecast</Table.Th>
              <Table.Th>Actual</Table.Th>
              <Table.Th>Status</Table.Th>
              {showActions && <Table.Th />}
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {milestones.map((m) => (
              <Table.Tr key={m.id}>
                <Table.Td>
                  <Text fw={600} size="sm">
                    {m.name}
                  </Text>
                  <Text size="xs" c="dimmed">
                    {milestoneCategoryLabel[m.category]}
                    {m.owner ? ` · ${m.owner}` : ''}
                  </Text>
                </Table.Td>
                <Table.Td>{formatDate(m.plannedDate, config.culture)}</Table.Td>
                <Table.Td>{formatDate(m.forecastDate, config.culture)}</Table.Td>
                <Table.Td>{formatDate(m.actualDate, config.culture)}</Table.Td>
                <Table.Td>
                  <MilestoneStatusBadge status={m.status} slippageDays={m.slippageDays} daysOverdue={m.daysOverdue} />
                </Table.Td>
                {showActions && (
                  <Table.Td>
                    <Group gap="xs" wrap="nowrap" justify="flex-end">
                      {canWrite && !m.actualDate && (
                        <Button
                          size="compact-sm"
                          variant="light"
                          loading={achievingId === m.id}
                          disabled={achievingId === m.id}
                          onClick={() => void markAchieved(m)}
                        >
                          Mark achieved
                        </Button>
                      )}
                      {canWrite && (
                        <Button size="compact-sm" variant="default" onClick={() => onEdit(m)}>
                          Edit
                        </Button>
                      )}
                      {canAdminister && (
                        <Button size="compact-sm" variant="subtle" color="red" onClick={() => setDeleting(m)}>
                          Delete
                        </Button>
                      )}
                    </Group>
                  </Table.Td>
                )}
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
      <Modal opened={deleting !== null} onClose={() => setDeleting(null)} title="Delete milestone" size="sm">
        <Text size="sm" mb="md">
          Delete &ldquo;{deleting?.name}&rdquo;? This cannot be undone.
        </Text>
        <Group>
          <Button color="red" loading={remove.isPending} onClick={() => void confirmDelete()}>
            Delete
          </Button>
          <Button variant="default" onClick={() => setDeleting(null)}>
            Cancel
          </Button>
        </Group>
      </Modal>
    </>
  );
}
