import { Alert, Anchor, Badge, Button, Card, Group, Loader, SimpleGrid, Stack, Table, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import type { ReactNode } from 'react';
import { Link, useParams } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { formatDate, formatDateTime, formatMoney } from '../../shared/format';
import { NotFoundPage } from '../../shared/layout/NotFoundPage';
import { usePermissions } from '../../shared/me';
import { useContractor, useContractorContracts, useSetContractorActive } from './api';
import { ComplianceBadge } from './ComplianceBadge';
import {
  contractFormLabel,
  contractorTypeLabel,
  contractStatusColor,
  contractStatusLabel,
  type ComplianceStatus,
  type ContractorDetails,
} from './types';

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

function ContractorActions({ contractor }: { contractor: ContractorDetails }) {
  const { canWrite, canAdminister } = usePermissions();
  const setActive = useSetContractorActive(contractor.id);

  return (
    <Group>
      {canWrite && (
        <Button component={Link} to={`/contractors/${contractor.id}/edit`} variant="light">
          Edit
        </Button>
      )}
      {canAdminister && (
        <Button
          variant="default"
          loading={setActive.isPending}
          onClick={() =>
            setActive.mutate(
              { active: !contractor.isActive, version: contractor.version },
              {
                onError: (error) =>
                  notifications.show({
                    color: 'red',
                    title:
                      error instanceof ApiError && error.status === 409
                        ? 'Someone else changed this contractor'
                        : 'Could not update the contractor',
                    message: 'Reload the page and try again.',
                  }),
              },
            )
          }
        >
          {contractor.isActive ? 'Deactivate' : 'Activate'}
        </Button>
      )}
    </Group>
  );
}

function ExpiryRow({ label, date, status, culture }: { label: string; date: string | null; status: ComplianceStatus; culture: string }) {
  return (
    <Table.Tr>
      <Table.Th>{label}</Table.Th>
      <Table.Td>{formatDate(date, culture)}</Table.Td>
      <Table.Td>
        <ComplianceBadge status={status} />
      </Table.Td>
    </Table.Tr>
  );
}

function ContractsCard({ contractorId }: { contractorId: string }) {
  const config = useConfig();
  const contracts = useContractorContracts(contractorId);

  return (
    <Card withBorder>
      <Title order={4} mb="sm">
        Contracts
      </Title>
      {contracts.error ? (
        <Alert color="red" title="Could not load contracts">
          {contracts.error.message}
        </Alert>
      ) : !contracts.data ? (
        <Loader size="sm" />
      ) : contracts.data.length === 0 ? (
        <Text c="dimmed">No contracts yet.</Text>
      ) : (
        <Table.ScrollContainer minWidth={720}>
          <Table striped>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Project</Table.Th>
                <Table.Th>Title</Table.Th>
                <Table.Th>Form</Table.Th>
                <Table.Th ta="right">Value</Table.Th>
                <Table.Th>Dates</Table.Th>
                <Table.Th>Status</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {contracts.data.map((c) => (
                <Table.Tr key={c.id}>
                  <Table.Td>
                    <Anchor component={Link} to={`/projects/${c.projectId}`}>
                      {[c.projectCode, c.projectName].filter(Boolean).join(' · ') || 'Project'}
                    </Anchor>
                  </Table.Td>
                  <Table.Td>{c.title}</Table.Td>
                  <Table.Td>{contractFormLabel[c.form]}</Table.Td>
                  <Table.Td ta="right" style={{ fontVariantNumeric: 'tabular-nums' }}>
                    {formatMoney(c.valueAmount, c.currency, config.culture)}
                  </Table.Td>
                  <Table.Td>
                    {formatDate(c.startDate, config.culture)} – {formatDate(c.endDate, config.culture)}
                  </Table.Td>
                  <Table.Td>
                    <Badge color={contractStatusColor[c.status]} variant="light">
                      {contractStatusLabel[c.status]}
                    </Badge>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
    </Card>
  );
}

export function ContractorDetailPage() {
  const { id = '' } = useParams();
  const contractor = useContractor(id);
  const config = useConfig();

  if (contractor.error instanceof ApiError && contractor.error.status === 404) {
    return <NotFoundPage message="This contractor doesn't exist or was removed." />;
  }
  if (contractor.error) {
    return (
      <Alert color="red" title="Could not load the contractor">
        {contractor.error.message}
      </Alert>
    );
  }
  if (!contractor.data) return <Loader />;

  const c = contractor.data;
  return (
    <Stack>
      <Group justify="space-between" align="flex-start">
        <Stack gap={4}>
          <Title order={2}>{c.name}</Title>
          <Group gap="xs">
            <Badge variant="outline">{contractorTypeLabel[c.type]}</Badge>
            <ComplianceBadge status={c.compliance} />
            {!c.isActive && <Badge color="gray">Inactive</Badge>}
          </Group>
        </Stack>
        <ContractorActions contractor={c} />
      </Group>

      <SimpleGrid cols={{ base: 1, md: 2 }}>
        <Card withBorder>
          <Title order={4} mb="sm">
            Contact
          </Title>
          <SimpleGrid cols={2}>
            <Field label="Contact name">{c.contactName}</Field>
            <Field label="Email">{c.email}</Field>
            <Field label="Phone">{c.phone}</Field>
            <Field label="Address">{c.address}</Field>
            <Field label="Trade">{c.trade}</Field>
            <Field label="NZBN">{c.nzbn}</Field>
          </SimpleGrid>
        </Card>

        <Card withBorder>
          <Title order={4} mb="sm">
            Compliance
          </Title>
          <Table>
            <Table.Tbody>
              <ExpiryRow label="Public liability" date={c.publicLiabilityExpiry} status={c.publicLiabilityStatus} culture={config.culture} />
              <ExpiryRow
                label="Professional indemnity"
                date={c.professionalIndemnityExpiry}
                status={c.professionalIndemnityStatus}
                culture={config.culture}
              />
              <ExpiryRow label="Licence" date={c.licenceExpiry} status={c.licenceStatus} culture={config.culture} />
            </Table.Tbody>
          </Table>
          <Field label="Licence number">{c.licenceNumber}</Field>
        </Card>
      </SimpleGrid>

      {c.notes && (
        <Card withBorder>
          <Title order={4} mb="sm">
            Notes
          </Title>
          <Text style={{ whiteSpace: 'pre-wrap' }}>{c.notes}</Text>
        </Card>
      )}

      <ContractsCard contractorId={c.id} />

      <Text size="sm" c="dimmed">
        Created by {c.createdBy} on {formatDateTime(c.createdAt, config.culture, config.timeZone)} · Last updated by{' '}
        {c.updatedBy} on {formatDateTime(c.updatedAt, config.culture, config.timeZone)}
      </Text>
    </Stack>
  );
}
