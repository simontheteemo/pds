import { Alert, Anchor, Badge, Button, Group, Loader, Select, Stack, Switch, Table, Text, TextInput, Title } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { useState } from 'react';
import { Link } from 'react-router';
import { usePermissions } from '../../shared/me';
import { useContractors } from './api';
import { ComplianceBadge } from './ComplianceBadge';
import { contractorTypeLabel, contractorTypes, type ContractorType } from './types';

export function ContractorsPage() {
  const { canWrite } = usePermissions();
  const [type, setType] = useState<ContractorType | null>(null);
  const [search, setSearch] = useState('');
  const [includeInactive, setIncludeInactive] = useState(false);
  const [debouncedSearch] = useDebouncedValue(search.trim(), 300);

  const contractors = useContractors({
    search: debouncedSearch || undefined,
    type: type ?? undefined,
    includeInactive: includeInactive || undefined,
  });
  const data = contractors.data;

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Contractors</Title>
        {canWrite && (
          <Button component={Link} to="/contractors/new">
            New contractor
          </Button>
        )}
      </Group>

      <Group align="flex-end" wrap="wrap">
        <TextInput
          label="Search"
          placeholder="Name or trade"
          value={search}
          onChange={(event) => setSearch(event.currentTarget.value)}
        />
        <Select
          label="Type"
          placeholder="All types"
          clearable
          data={contractorTypes.map((value) => ({ value, label: contractorTypeLabel[value] }))}
          value={type}
          onChange={(value) => setType(contractorTypes.find((t) => t === value) ?? null)}
        />
        <Switch
          label="Show inactive"
          checked={includeInactive}
          onChange={(event) => setIncludeInactive(event.currentTarget.checked)}
        />
      </Group>

      {contractors.error ? (
        <Alert color="red" title="Could not load contractors">
          {contractors.error.message}
        </Alert>
      ) : !data ? (
        <Loader />
      ) : data.length === 0 ? (
        <Text c="dimmed">No contractors match these filters.</Text>
      ) : (
        <Table.ScrollContainer minWidth={640}>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Name</Table.Th>
                <Table.Th>Type</Table.Th>
                <Table.Th>Trade</Table.Th>
                <Table.Th>Compliance</Table.Th>
                <Table.Th>Status</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {data.map((contractor) => (
                <Table.Tr key={contractor.id}>
                  <Table.Td>
                    <Anchor component={Link} to={`/contractors/${contractor.id}`}>
                      {contractor.name}
                    </Anchor>
                  </Table.Td>
                  <Table.Td>{contractorTypeLabel[contractor.type]}</Table.Td>
                  <Table.Td>{contractor.trade ?? '—'}</Table.Td>
                  <Table.Td>
                    <ComplianceBadge status={contractor.compliance} />
                  </Table.Td>
                  <Table.Td>
                    {contractor.isActive ? (
                      <Badge color="green" variant="outline">
                        Active
                      </Badge>
                    ) : (
                      <Badge color="gray" variant="outline">
                        Inactive
                      </Badge>
                    )}
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
    </Stack>
  );
}
