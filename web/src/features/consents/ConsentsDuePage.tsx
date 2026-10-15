import { Alert, Anchor, Button, Group, Loader, Stack, Table, Text, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Link } from 'react-router';
import { useConfig } from '../../shared/config';
import { formatDate } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { useDueItems } from './api';
import { DueStateBadge } from './DueStateBadge';
import { ReminderPreviewModal } from './ReminderPreviewModal';
import { consentTypeLabel, dueStateLabel, expiryStateLabel } from './types';

export function ConsentsDuePage() {
  const config = useConfig();
  const due = useDueItems();
  const items = due.data;
  const { canAdminister } = usePermissions();
  const [opened, { open, close }] = useDisclosure(false);

  return (
    <Stack>
      <Group justify="space-between" align="flex-start">
        <div>
          <Title order={2}>Consents due</Title>
          <Text c="dimmed">
            Overdue and due within 30 days (consent expiries within 90 days) across active projects.
          </Text>
        </div>
        {canAdminister && (
          <Button variant="default" onClick={open}>
            Preview reminder email
          </Button>
        )}
      </Group>
      {canAdminister && <ReminderPreviewModal opened={opened} onClose={close} />}

      {due.error ? (
        <Alert color="red" title="Could not load due consents">
          {due.error.message}
        </Alert>
      ) : !items ? (
        <Loader />
      ) : items.length === 0 ? (
        <Text c="dimmed">Nothing overdue or due soon.</Text>
      ) : (
        <Table.ScrollContainer minWidth={720}>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Date</Table.Th>
                <Table.Th>State</Table.Th>
                <Table.Th>Project</Table.Th>
                <Table.Th>Consent</Table.Th>
                <Table.Th>Item</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {items.map((item) => (
                <Table.Tr key={`${item.consentId}-${item.conditionId ?? 'expiry'}`}>
                  <Table.Td>{formatDate(item.date, config.culture)}</Table.Td>
                  <Table.Td>
                    <DueStateBadge
                      state={item.state}
                      label={
                        item.kind === 'Expiry' ? expiryStateLabel[item.state] : dueStateLabel[item.state]
                      }
                    />
                  </Table.Td>
                  <Table.Td>
                    <Anchor component={Link} to={`/projects/${item.projectId}`}>
                      {item.projectCode} · {item.projectName}
                    </Anchor>
                  </Table.Td>
                  <Table.Td>
                    <Text size="sm" c="dimmed">
                      {consentTypeLabel[item.consentType]}
                    </Text>
                    <Text>{item.consentTitle}</Text>
                    {item.consentReference && (
                      <Text size="sm" c="dimmed">
                        {item.consentReference}
                      </Text>
                    )}
                  </Table.Td>
                  <Table.Td>
                    {item.kind === 'Expiry' ? (
                      <Text>Consent expires</Text>
                    ) : (
                      <>
                        {item.conditionReference && <Text fw={600}>{item.conditionReference}</Text>}
                        <Text>{item.description}</Text>
                      </>
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
