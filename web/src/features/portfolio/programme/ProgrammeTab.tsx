import { Alert, Anchor, Badge, Button, Card, Group, Loader, Stack, Table, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { Link } from 'react-router';
import { useConfig } from '../../../shared/config';
import { formatDate } from '../../../shared/format';
import { usePermissions } from '../../../shared/me';
import type { ProjectTabProps } from '../workspace/projectTabs';
import { useAddStandardMilestones, useMilestones } from './api';
import { MilestoneFormModal } from './MilestoneFormModal';
import { MilestonesTable } from './MilestonesTable';
import { isSlipping, type MilestoneDetails } from './types';

export function ProgrammeTab({ project: p }: ProjectTabProps) {
  const config = useConfig();
  const { canWrite, canAdminister } = usePermissions();
  const milestones = useMilestones(p.id);
  const addStandard = useAddStandardMilestones(p.id);
  const [editing, setEditing] = useState<MilestoneDetails | 'new' | undefined>(undefined);
  const writable = canWrite && !p.isArchived;
  const items = milestones.data?.items ?? [];
  const slipping = items.filter((m) => isSlipping(m.status)).length;
  const complete = items.filter((m) => m.status === 'Complete').length;

  const standard = async () => {
    try {
      const added = await addStandard.mutateAsync();
      notifications.show({
        color: 'green',
        message: added.length > 0 ? `Added ${added.length} milestones` : 'All standard milestones already exist',
      });
    } catch (error) {
      notifications.show({
        color: 'red',
        title: 'Could not add standard milestones',
        message: error instanceof Error ? error.message : 'Unexpected error',
      });
    }
  };
  const standardButton = (variant: string) => (
    <Button variant={variant} loading={addStandard.isPending} onClick={() => void standard()}>
      Add standard milestones
    </Button>
  );

  return (
    <Stack>
      <Card withBorder padding="sm">
        <Title order={5} mb="xs">
          Project dates
        </Title>
        <Table verticalSpacing="xs">
          <Table.Thead>
            <Table.Tr>
              <Table.Th />
              <Table.Th>Start</Table.Th>
              <Table.Th>Completion</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            <Table.Tr>
              <Table.Th>Planned</Table.Th>
              <Table.Td>{formatDate(p.plannedStart, config.culture)}</Table.Td>
              <Table.Td>{formatDate(p.plannedCompletion, config.culture)}</Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Th>Actual</Table.Th>
              <Table.Td>{formatDate(p.actualStart, config.culture)}</Table.Td>
              <Table.Td>{formatDate(p.actualCompletion, config.culture)}</Table.Td>
            </Table.Tr>
          </Table.Tbody>
        </Table>
        <Text size="xs" c="dimmed" mt="xs">
          Cashflow spreads costs over these dates. Milestones below are the detailed programme.
        </Text>
        {canWrite && !p.isArchived && (
          <Anchor component={Link} to={`/projects/${p.id}/edit`} size="sm" mt="xs" display="block">
            Edit project dates
          </Anchor>
        )}
      </Card>
      <Card withBorder>
        <Group justify="space-between" mb="sm" align="flex-start">
          <Group gap="xs">
            <Title order={4}>Milestones</Title>
            {items.length > 0 && (
              <>
                {slipping > 0 && <Badge color="red" variant="light">{slipping} slipping</Badge>}
                <Badge color="teal" variant="light">
                  {complete} complete of {items.length}
                </Badge>
              </>
            )}
          </Group>
          {writable && (
            <Group gap="xs">
              {standardButton('default')}
              <Button onClick={() => setEditing('new')}>Add milestone</Button>
            </Group>
          )}
        </Group>
        {milestones.isPending ? (
          <Loader size="sm" />
        ) : milestones.isError ? (
          <Alert color="red">Could not load milestones.</Alert>
        ) : items.length === 0 ? (
          <Stack align="flex-start" gap="xs">
            <Text c="dimmed">No milestones yet. Add the standard NZ development milestones to get started.</Text>
            {writable && standardButton('filled')}
          </Stack>
        ) : (
          <MilestonesTable
            projectId={p.id}
            milestones={items}
            today={milestones.data.today}
            canWrite={writable}
            canAdminister={canAdminister && !p.isArchived}
            onEdit={setEditing}
          />
        )}
      </Card>
      <MilestoneFormModal projectId={p.id} milestones={items} milestone={editing} onClose={() => setEditing(undefined)} />
    </Stack>
  );
}
