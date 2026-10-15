import { Alert, Anchor, Badge, Button, Card, Group, Loader, Table, Text, Title } from '@mantine/core';
import { useState } from 'react';
import { Link } from 'react-router';
import { useConfig } from '../../shared/config';
import { formatDate, formatMoney } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { useProjectContracts } from './api';
import { ContractFormModal } from './ContractFormModal';
import { contractFormLabel, contractStatusColor, contractStatusLabel, type ProjectContract } from './types';

export function ProjectContractsSection({ projectId, projectArchived }: { projectId: string; projectArchived: boolean }) {
  const config = useConfig();
  const { canWrite } = usePermissions();
  const [editing, setEditing] = useState<ProjectContract | 'new' | undefined>(undefined);
  const contracts = useProjectContracts(projectId);

  return (
    <Card withBorder>
      <Group justify="space-between" mb="sm">
        <Title order={4}>Contracts</Title>
        {canWrite && !projectArchived && <Button onClick={() => setEditing('new')}>Add contract</Button>}
      </Group>

      {contracts.error ? (
        <Alert color="red" title="Could not load contracts">{contracts.error.message}</Alert>
      ) : !contracts.data ? (
        <Loader size="sm" />
      ) : contracts.data.length === 0 ? (
        <Text c="dimmed">No contracts recorded.</Text>
      ) : (
        <Table.ScrollContainer minWidth={820}>
          <Table highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Contractor</Table.Th>
                <Table.Th>Title</Table.Th>
                <Table.Th>Reference</Table.Th>
                <Table.Th>Form</Table.Th>
                <Table.Th>Value</Table.Th>
                <Table.Th>Start–End</Table.Th>
                <Table.Th>Status</Table.Th>
                {canWrite && <Table.Th />}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {contracts.data.map((c) => (
                <Table.Tr key={c.id}>
                  <Table.Td>
                    <Anchor component={Link} to={`/contractors/${c.contractorId}`}>{c.contractorName}</Anchor>
                  </Table.Td>
                  <Table.Td>{c.title}</Table.Td>
                  <Table.Td>{c.reference ?? '—'}</Table.Td>
                  <Table.Td>{contractFormLabel[c.form]}</Table.Td>
                  <Table.Td>{formatMoney(c.valueAmount, c.currency, config.culture)}</Table.Td>
                  <Table.Td>
                    {formatDate(c.startDate, config.culture)} – {formatDate(c.endDate, config.culture)}
                  </Table.Td>
                  <Table.Td>
                    <Badge color={contractStatusColor[c.status]} variant="light">{contractStatusLabel[c.status]}</Badge>
                  </Table.Td>
                  {canWrite && (
                    <Table.Td>
                      <Button size="xs" variant="light" onClick={() => setEditing(c)}>Edit</Button>
                    </Table.Td>
                  )}
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}

      <ContractFormModal projectId={projectId} contract={editing} onClose={() => setEditing(undefined)} />
    </Card>
  );
}
