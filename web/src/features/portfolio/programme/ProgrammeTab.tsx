import { Anchor, Card, Stack, Table, Text, Title } from '@mantine/core';
import { Link } from 'react-router';
import { useConfig } from '../../../shared/config';
import { formatDate } from '../../../shared/format';
import { usePermissions } from '../../../shared/me';
import type { ProjectTabProps } from '../workspace/projectTabs';

export function ProgrammeTab({ project: p }: ProjectTabProps) {
  const config = useConfig();
  const { canWrite } = usePermissions();

  return (
    <Stack>
      <Card withBorder>
        <Title order={4} mb="sm">
          Project dates
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
        <Text size="sm" c="dimmed" mt="sm">
          Cashflow spreads costs over these dates. Milestones below are the detailed programme.
        </Text>
        {canWrite && !p.isArchived && (
          <Anchor component={Link} to={`/projects/${p.id}/edit`} size="sm" mt="xs" display="block">
            Edit project dates
          </Anchor>
        )}
      </Card>
    </Stack>
  );
}
