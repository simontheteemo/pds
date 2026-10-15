import { Button, Group, Table, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { formatDate } from '../../shared/format';
import { useReopenCondition } from './api';
import { CompleteConditionModal } from './CompleteConditionModal';
import { ConditionFormModal } from './ConditionFormModal';
import { DueStateBadge } from './DueStateBadge';
import type { ConditionDetails, ConsentDetails } from './types';

export function ConsentConditionsTable({
  projectId,
  consent,
  canWrite,
  projectArchived,
}: {
  projectId: string;
  consent: ConsentDetails;
  canWrite: boolean;
  projectArchived: boolean;
}) {
  const config = useConfig();
  const [editing, setEditing] = useState<ConditionDetails | 'new' | undefined>(undefined);
  const [completing, setCompleting] = useState<ConditionDetails | undefined>(undefined);
  const reopen = useReopenCondition(projectId);
  const conditions = consent.conditions;

  const onReopen = (c: ConditionDetails) =>
    reopen.mutate(
      { consentId: consent.id, conditionId: c.id, version: c.version },
      {
        onError: (error) =>
          notifications.show({
            color: error instanceof ApiError && error.status === 409 ? 'yellow' : 'red',
            title:
              error instanceof ApiError && error.status === 409
                ? 'This condition was changed by someone else'
                : 'Could not reopen the condition',
            message:
              error instanceof ApiError && error.status === 409
                ? 'The list now shows the latest version.'
                : error.message,
          }),
      },
    );

  return (
    <div>
      <Group justify="space-between" mb="xs">
        <Title order={5}>Conditions</Title>
        {canWrite && !projectArchived && (
          <Button size="xs" onClick={() => setEditing('new')}>
            Add condition
          </Button>
        )}
      </Group>

      {conditions.length === 0 ? (
        <Text c="dimmed" size="sm">
          No conditions recorded.
        </Text>
      ) : (
        <Table.ScrollContainer minWidth={640}>
          <Table highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Ref</Table.Th>
                <Table.Th>Description</Table.Th>
                <Table.Th>Due</Table.Th>
                <Table.Th>State</Table.Th>
                {canWrite && <Table.Th>Actions</Table.Th>}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {conditions.map((c) => (
                <Table.Tr key={c.id} opacity={c.dueState === 'Done' ? 0.6 : 1}>
                  <Table.Td>{c.reference ?? '—'}</Table.Td>
                  <Table.Td>
                    <Text size="sm" lineClamp={3} style={{ whiteSpace: 'pre-wrap' }}>
                      {c.description}
                    </Text>
                  </Table.Td>
                  <Table.Td>{formatDate(c.dueDate, config.culture)}</Table.Td>
                  <Table.Td>
                    <Group gap="xs" wrap="nowrap">
                      <DueStateBadge state={c.dueState} />
                      {c.dueState === 'Done' && c.completedOn && (
                        <Text size="sm" c="dimmed">
                          on {formatDate(c.completedOn, config.culture)}
                        </Text>
                      )}
                    </Group>
                  </Table.Td>
                  {canWrite && (
                    <Table.Td>
                      <Group gap="xs" justify="flex-end" wrap="nowrap">
                        <Button size="xs" variant="light" onClick={() => setEditing(c)}>
                          Edit
                        </Button>
                        {c.dueState === 'Done' ? (
                          <Button
                            size="xs"
                            variant="default"
                            loading={reopen.isPending && reopen.variables?.conditionId === c.id}
                            onClick={() => onReopen(c)}
                          >
                            Reopen
                          </Button>
                        ) : (
                          <Button size="xs" variant="default" onClick={() => setCompleting(c)}>
                            Complete
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
      )}

      <ConditionFormModal
        projectId={projectId}
        consentId={consent.id}
        condition={editing}
        onClose={() => setEditing(undefined)}
      />
      <CompleteConditionModal
        projectId={projectId}
        consentId={consent.id}
        condition={completing}
        onClose={() => setCompleting(undefined)}
      />
    </div>
  );
}
