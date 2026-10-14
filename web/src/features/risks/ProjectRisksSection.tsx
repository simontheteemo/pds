import { Alert, Button, Card, Group, Loader, Switch, Table, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { formatDate } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { useReopenRisk, useRisks } from './api';
import { CloseRiskModal } from './CloseRiskModal';
import { RiskBandBadge } from './RiskBandBadge';
import { RiskFormModal } from './RiskFormModal';
import { categoryLabel, statusLabel, type RiskDetails } from './types';

export function ProjectRisksSection({ projectId, projectArchived }: { projectId: string; projectArchived: boolean }) {
  const config = useConfig();
  const { canWrite } = usePermissions();
  const [showClosed, setShowClosed] = useState(false);
  const [editing, setEditing] = useState<RiskDetails | 'new' | undefined>(undefined);
  const [closing, setClosing] = useState<RiskDetails | undefined>(undefined);
  const risks = useRisks(projectId, showClosed);
  const reopen = useReopenRisk(projectId);

  const onReopen = (risk: RiskDetails) =>
    reopen.mutate(
      { riskId: risk.id, version: risk.version },
      {
        onError: (error) =>
          notifications.show({
            color: 'red',
            title: error instanceof ApiError && error.status === 409 ? 'This risk was changed by someone else' : 'Could not reopen the risk',
            message:
              error instanceof ApiError && error.status === 409
                ? 'The list now shows the latest version.'
                : error.message,
          }),
      },
    );

  return (
    <Card withBorder>
      <Group justify="space-between" mb="sm">
        <Title order={4}>Risks</Title>
        <Group>
          <Switch label="Show closed" checked={showClosed} onChange={(e) => setShowClosed(e.currentTarget.checked)} />
          {canWrite && !projectArchived && <Button onClick={() => setEditing('new')}>Add risk</Button>}
        </Group>
      </Group>

      {risks.error ? (
        <Alert color="red" title="Could not load risks">{risks.error.message}</Alert>
      ) : !risks.data ? (
        <Loader size="sm" />
      ) : risks.data.length === 0 ? (
        <Text c="dimmed">{showClosed ? 'No risks recorded.' : 'No open risks recorded.'}</Text>
      ) : (
        <Table.ScrollContainer minWidth={720}>
          <Table highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Rating</Table.Th>
                <Table.Th>Title</Table.Th>
                <Table.Th>Category</Table.Th>
                <Table.Th>Owner</Table.Th>
                <Table.Th>Due</Table.Th>
                <Table.Th>Status</Table.Th>
                {canWrite && <Table.Th>Actions</Table.Th>}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {risks.data.map((risk) => (
                <Table.Tr key={risk.id} opacity={risk.status === 'Closed' ? 0.6 : 1}>
                  <Table.Td><RiskBandBadge band={risk.band} score={risk.score} /></Table.Td>
                  <Table.Td>
                    <Text fw={500}>{risk.title}</Text>
                    {risk.mitigation && <Text size="sm" c="dimmed" lineClamp={1}>{risk.mitigation}</Text>}
                  </Table.Td>
                  <Table.Td>{categoryLabel[risk.category]}</Table.Td>
                  <Table.Td>{risk.owner ?? '—'}</Table.Td>
                  <Table.Td>{formatDate(risk.dueDate, config.culture)}</Table.Td>
                  <Table.Td>{statusLabel[risk.status]}</Table.Td>
                  {canWrite && (
                    <Table.Td>
                      <Group gap="xs" justify="flex-end" wrap="nowrap">
                        {risk.status === 'Closed' ? (
                          <Button
                            size="xs"
                            variant="default"
                            loading={reopen.isPending && reopen.variables?.riskId === risk.id}
                            onClick={() => onReopen(risk)}
                          >
                            Reopen
                          </Button>
                        ) : (
                          <>
                            <Button size="xs" variant="light" onClick={() => setEditing(risk)}>Edit</Button>
                            <Button size="xs" variant="default" onClick={() => setClosing(risk)}>Close</Button>
                          </>
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

      <RiskFormModal projectId={projectId} risk={editing} onClose={() => setEditing(undefined)} />
      <CloseRiskModal projectId={projectId} risk={closing} onClose={() => setClosing(undefined)} />
    </Card>
  );
}
