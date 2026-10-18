import { Alert, Badge, Button, Group, Loader, Stack, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { Link, useParams } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { formatDateTime } from '../../shared/format';
import { NotFoundPage } from '../../shared/layout/NotFoundPage';
import { usePermissions } from '../../shared/me';
import { useProject, useSetArchived } from './api';
import { StatusBadge } from './StatusBadge';
import { stageLabel, type ProjectDetails } from './types';
import { ProjectWorkspaceTabs } from './workspace/ProjectWorkspaceTabs';

export function ProjectActions({ project }: { project: ProjectDetails }) {
  const { canWrite, canAdminister } = usePermissions();
  const setArchived = useSetArchived(project.id);

  return (
    <Group>
      {canWrite && (
        <Button component={Link} to={`/projects/${project.id}/edit`} variant="light">
          Edit
        </Button>
      )}
      {canAdminister && (
        <Button
          variant="default"
          loading={setArchived.isPending}
          onClick={() =>
            setArchived.mutate(
              { archived: !project.isArchived, version: project.version },
              {
                onError: (error) =>
                  notifications.show({
                    color: 'red',
                    title:
                      error instanceof ApiError && error.status === 409
                        ? 'Someone else changed this project'
                        : 'Could not update the project',
                    message: 'Reload the page and try again.',
                  }),
              },
            )
          }
        >
          {project.isArchived ? 'Restore' : 'Archive'}
        </Button>
      )}
    </Group>
  );
}

export function ProjectDetailPage() {
  const { id = '' } = useParams();
  const project = useProject(id);
  const config = useConfig();

  if (project.error instanceof ApiError && project.error.status === 404) {
    return <NotFoundPage message="This project doesn't exist or was removed." />;
  }
  if (project.error) {
    return (
      <Alert color="red" title="Could not load the project">
        {project.error.message}
      </Alert>
    );
  }
  if (!project.data) return <Loader />;

  const p = project.data;
  return (
    <Stack>
      <Group justify="space-between" align="flex-start">
        <Stack gap={4}>
          <Title order={2}>
            {p.code} · {p.name}
          </Title>
          <Group gap="xs">
            <StatusBadge status={p.status} />
            <Badge variant="outline">{stageLabel[p.stage]}</Badge>
            {p.isArchived && <Badge color="gray">Archived</Badge>}
          </Group>
        </Stack>
        <ProjectActions project={p} />
      </Group>

      <ProjectWorkspaceTabs project={p} />

      <Text size="sm" c="dimmed">
        Created by {p.createdBy} on {formatDateTime(p.createdAt, config.culture, config.timeZone)} · Last updated by{' '}
        {p.updatedBy} on {formatDateTime(p.updatedAt, config.culture, config.timeZone)}
      </Text>
    </Stack>
  );
}
