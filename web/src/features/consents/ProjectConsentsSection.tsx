import { Accordion, Alert, Badge, Button, Card, Group, Loader, SimpleGrid, Text, Title } from '@mantine/core';
import { useState } from 'react';
import { useConfig } from '../../shared/config';
import { formatDate } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { useConsents } from './api';
import { ConsentConditionsTable } from './ConsentConditionsTable';
import { ConsentFormModal } from './ConsentFormModal';
import { DueStateBadge } from './DueStateBadge';
import {
  consentStatusColor,
  consentStatusLabel,
  consentTypeLabel,
  expiryStateLabel,
  type ConsentDetails,
} from './types';

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <Text size="xs" c="dimmed">
        {label}
      </Text>
      <Text size="sm" component="div">
        {children}
      </Text>
    </div>
  );
}

export function ProjectConsentsSection({
  projectId,
  projectArchived,
}: {
  projectId: string;
  projectArchived: boolean;
}) {
  const config = useConfig();
  const { canWrite } = usePermissions();
  const [editing, setEditing] = useState<ConsentDetails | 'new' | undefined>(undefined);
  const consents = useConsents(projectId);

  return (
    <Card withBorder>
      <Group justify="space-between" mb="sm">
        <Title order={4}>Consents</Title>
        {canWrite && !projectArchived && <Button onClick={() => setEditing('new')}>Add consent</Button>}
      </Group>

      {consents.error ? (
        <Alert color="red" title="Could not load consents">
          {consents.error.message}
        </Alert>
      ) : !consents.data ? (
        <Loader size="sm" />
      ) : consents.data.length === 0 ? (
        <Text c="dimmed">No consents recorded.</Text>
      ) : (
        <Accordion multiple chevronPosition="left" variant="separated">
          {consents.data.map((c) => (
            <Accordion.Item key={c.id} value={c.id}>
              <Accordion.Control>
                <Group gap="sm" opacity={c.isActive ? 1 : 0.6}>
                  <Text c="dimmed" size="sm">
                    {consentTypeLabel[c.type]}
                  </Text>
                  <Text fw={500}>{c.title}</Text>
                  {c.reference && (
                    <Text c="dimmed" size="sm">
                      {c.reference}
                    </Text>
                  )}
                  <Badge color={consentStatusColor[c.status]} variant="light">
                    {consentStatusLabel[c.status]}
                  </Badge>
                  <DueStateBadge state={c.attention} />
                  {c.conditionCount > 0 && (
                    <Text size="sm" c="dimmed">
                      {c.openConditionCount} open of {c.conditionCount} conditions
                    </Text>
                  )}
                </Group>
              </Accordion.Control>
              <Accordion.Panel>
                <SimpleGrid cols={{ base: 1, sm: 4 }} mb="sm">
                  <Field label="Authority">{c.authority ?? '—'}</Field>
                  <Field label="Lodged">{formatDate(c.lodgedDate, config.culture)}</Field>
                  <Field label="Granted">{formatDate(c.grantedDate, config.culture)}</Field>
                  <Field label="Expiry">
                    <Group gap="xs">
                      {formatDate(c.expiryDate, config.culture)}
                      {c.expiryState !== 'None' && (
                        <DueStateBadge state={c.expiryState} label={expiryStateLabel[c.expiryState]} />
                      )}
                    </Group>
                  </Field>
                </SimpleGrid>
                {c.notes && (
                  <Text size="sm" mb="sm" style={{ whiteSpace: 'pre-wrap' }}>
                    {c.notes}
                  </Text>
                )}
                {canWrite && (
                  <Button size="xs" variant="light" mb="sm" onClick={() => setEditing(c)}>
                    Edit
                  </Button>
                )}
                <ConsentConditionsTable
                  projectId={projectId}
                  consent={c}
                  canWrite={canWrite}
                  projectArchived={projectArchived}
                />
              </Accordion.Panel>
            </Accordion.Item>
          ))}
        </Accordion>
      )}

      <ConsentFormModal projectId={projectId} consent={editing} onClose={() => setEditing(undefined)} />
    </Card>
  );
}
