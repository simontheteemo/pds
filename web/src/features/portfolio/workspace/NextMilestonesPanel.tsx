import { Alert, Anchor, Button, Card, Group, Loader, Stack, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { Link } from 'react-router';
import { useConfig } from '../../../shared/config';
import { formatDate } from '../../../shared/format';
import { usePermissions } from '../../../shared/me';
import { useAddStandardMilestones, useNextMilestones } from '../programme/api';
import { MilestoneStatusBadge } from '../programme/MilestoneStatusBadge';
import { dueDate } from '../programme/types';
import type { OverviewPanelProps } from './overviewPanels';
import { useProjectTabLink } from './ProjectWorkspaceTabs';

export function NextMilestonesPanel({ project }: OverviewPanelProps) {
  const config = useConfig();
  const tabLink = useProjectTabLink();
  const { canWrite } = usePermissions();
  const { items, isPending, isError } = useNextMilestones(project.id, 5);
  const addStandard = useAddStandardMilestones(project.id);

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

  return (
    <Card withBorder h="100%">
      <Title order={4} mb="sm">
        Next milestones
      </Title>
      {isPending ? (
        <Loader size="sm" />
      ) : isError ? (
        <Alert color="red">Could not load milestones.</Alert>
      ) : items.length === 0 ? (
        <Stack align="flex-start" gap="xs">
          <Text c="dimmed">No upcoming milestones.</Text>
          {canWrite && !project.isArchived && (
            <Button variant="default" loading={addStandard.isPending} onClick={() => void standard()}>
              Add standard milestones
            </Button>
          )}
        </Stack>
      ) : (
        <Stack gap="xs">
          {items.map((m) => {
            const date = dueDate(m);
            return (
              <Group key={m.id} justify="space-between" wrap="nowrap" align="flex-start" gap="xs">
                <div style={{ minWidth: 0 }}>
                  <Text size="sm" lineClamp={2}>
                    {m.name}
                  </Text>
                  <Text size="xs" c="dimmed">
                    {date ? formatDate(date, config.culture) : 'Not scheduled'}
                  </Text>
                </div>
                <MilestoneStatusBadge status={m.status} slippageDays={m.slippageDays} daysOverdue={m.daysOverdue} />
              </Group>
            );
          })}
        </Stack>
      )}
      <Anchor component={Link} to={tabLink('programme')} size="sm" mt="sm" display="block">
        Open programme
      </Anchor>
    </Card>
  );
}
