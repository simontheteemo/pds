import { Alert, Anchor, Badge, Button, Group, Loader, Pagination, Select, Stack, Switch, Table, Text, TextInput, Title } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { useState } from 'react';
import { Link } from 'react-router';
import { useConfig } from '../../shared/config';
import { formatDate, formatMoney } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { useProjects } from './api';
import { StatusBadge } from './StatusBadge';
import { stageLabel, stages, statuses, statusLabel, type Stage, type Status } from './types';

const PAGE_SIZE = 25;

export function ProjectsPage() {
  const config = useConfig();
  const { canWrite, canAdminister } = usePermissions();
  const [stage, setStage] = useState<Stage | null>(null);
  const [status, setStatus] = useState<Status | null>(null);
  const [search, setSearch] = useState('');
  const [includeArchived, setIncludeArchived] = useState(false);
  const [page, setPage] = useState(1);
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300);

  const projects = useProjects({
    stage: stage ?? undefined,
    status: status ?? undefined,
    search: debouncedSearch || undefined,
    includeArchived: includeArchived || undefined,
    page,
    pageSize: PAGE_SIZE,
  });
  const data = projects.data;
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Projects</Title>
        {canWrite && (
          <Button component={Link} to="/projects/new">
            New project
          </Button>
        )}
      </Group>

      <Group align="flex-end" wrap="wrap">
        <TextInput
          label="Search"
          placeholder="Code, name, suburb or city"
          value={search}
          onChange={(event) => {
            setSearch(event.currentTarget.value);
            setPage(1);
          }}
        />
        <Select
          label="Stage"
          placeholder="All stages"
          clearable
          data={stages.map((value) => ({ value, label: stageLabel[value] }))}
          value={stage}
          onChange={(value) => {
            setStage(value as Stage | null);
            setPage(1);
          }}
        />
        <Select
          label="Status"
          placeholder="All statuses"
          clearable
          data={statuses.map((value) => ({ value, label: statusLabel[value] }))}
          value={status}
          onChange={(value) => {
            setStatus(value as Status | null);
            setPage(1);
          }}
        />
        {canAdminister && (
          <Switch
            label="Show archived"
            checked={includeArchived}
            onChange={(event) => {
              setIncludeArchived(event.currentTarget.checked);
              setPage(1);
            }}
          />
        )}
      </Group>

      {projects.error ? (
        <Alert color="red" title="Could not load projects">
          {projects.error.message}
        </Alert>
      ) : !data ? (
        <Loader />
      ) : data.items.length === 0 ? (
        <Text c="dimmed">No projects match these filters.</Text>
      ) : (
        <Table.ScrollContainer minWidth={760}>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Code</Table.Th>
                <Table.Th>Name</Table.Th>
                <Table.Th>Stage</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Location</Table.Th>
                <Table.Th>Planned completion</Table.Th>
                <Table.Th ta="right">Budget</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {data.items.map((project) => (
                <Table.Tr key={project.id}>
                  <Table.Td>
                    <Anchor component={Link} to={`/projects/${project.id}`}>
                      {project.code}
                    </Anchor>
                  </Table.Td>
                  <Table.Td>
                    {project.name}
                    {project.isArchived && (
                      <Badge ml="xs" color="gray" variant="outline">
                        Archived
                      </Badge>
                    )}
                  </Table.Td>
                  <Table.Td>{stageLabel[project.stage]}</Table.Td>
                  <Table.Td>
                    <StatusBadge status={project.status} />
                  </Table.Td>
                  <Table.Td>{[project.suburb, project.city].filter(Boolean).join(', ')}</Table.Td>
                  <Table.Td>{formatDate(project.plannedCompletion, config.culture)}</Table.Td>
                  <Table.Td ta="right" style={{ fontVariantNumeric: 'tabular-nums' }}>
                    {formatMoney(project.budgetAmount, project.currency, config.culture)}
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}

      {totalPages > 1 && <Pagination total={totalPages} value={page} onChange={setPage} />}
    </Stack>
  );
}
