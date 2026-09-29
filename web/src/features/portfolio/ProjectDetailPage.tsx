import { Alert, Badge, Button, Card, Group, Loader, SimpleGrid, Stack, Table, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import type { ReactNode } from 'react';
import { Link, useParams } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { formatDate, formatDateTime, formatMoney } from '../../shared/format';
import { NotFoundPage } from '../../shared/layout/NotFoundPage';
import { usePermissions } from '../../shared/me';
import { useProject, useSetArchived } from './api';
import { StatusBadge } from './StatusBadge';
import { stageLabel, type ProjectDetails } from './types';

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      <Text>{children || '—'}</Text>
    </div>
  );
}

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

      <SimpleGrid cols={{ base: 1, md: 2 }}>
        <Card withBorder>
          <Title order={4} mb="sm">
            Site
          </Title>
          <SimpleGrid cols={2}>
            <Field label="Address">{p.site.addressLine}</Field>
            <Field label="Suburb">{p.site.suburb}</Field>
            <Field label="City">{p.site.city}</Field>
            <Field label="Region">{p.site.region}</Field>
            <Field label="Legal description">{p.site.legalDescription}</Field>
            <Field label="Title reference">{p.site.titleReference}</Field>
            <Field label="Land area">
              {p.site.landAreaSqm === null ? null : `${p.site.landAreaSqm.toLocaleString(config.culture)} m²`}
            </Field>
            <Field label="Postcode">{p.site.postcode}</Field>
          </SimpleGrid>
        </Card>

        <Card withBorder>
          <Title order={4} mb="sm">
            Timeline and budget
          </Title>
          <Table>
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
          <SimpleGrid cols={2} mt="md">
            <Field label="Budget">{formatMoney(p.budgetAmount, p.currency, config.culture)}</Field>
            <Field label="Project manager">{p.projectManager}</Field>
          </SimpleGrid>
        </Card>
      </SimpleGrid>

      {p.description && (
        <Card withBorder>
          <Title order={4} mb="sm">
            Description
          </Title>
          <Text style={{ whiteSpace: 'pre-wrap' }}>{p.description}</Text>
        </Card>
      )}

      <Text size="sm" c="dimmed">
        Created by {p.createdBy} on {formatDateTime(p.createdAt, config.culture, config.timeZone)} · Last updated by{' '}
        {p.updatedBy} on {formatDateTime(p.updatedAt, config.culture, config.timeZone)}
      </Text>
    </Stack>
  );
}
