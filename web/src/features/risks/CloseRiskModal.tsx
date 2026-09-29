import { Alert, Button, Group, Modal, Stack, Text, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { z } from 'zod';
import { ApiError } from '../../shared/api/client';
import { serverFieldErrors, zodValidate } from '../../shared/validation';
import { useCloseRisk } from './api';
import type { RiskDetails } from './types';

const closeSchema = z.object({
  note: z.string().trim().min(1, 'Explain why the risk is being closed').max(2000, 'Use 2000 characters or fewer'),
});

type CloseFormValues = { note: string };

type Props = {
  projectId: string;
  risk: RiskDetails | undefined;
  onClose: () => void;
};

function CloseRiskModalInner({ projectId, risk, onClose }: { projectId: string; risk: RiskDetails; onClose: () => void }) {
  const closeRisk = useCloseRisk(projectId);
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const form = useForm<CloseFormValues>({
    mode: 'controlled',
    initialValues: { note: '' },
    validate: zodValidate<CloseFormValues>(closeSchema),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      await closeRisk.mutateAsync({ riskId: risk.id, version: risk.version, note: values.note.trim() });
      notifications.show({ color: 'green', message: 'Risk closed' });
      onClose();
    } catch (error) {
      if (error instanceof ApiError && error.status === 400 && error.problem?.errors) {
        const fieldErrors = serverFieldErrors(error.problem);
        form.setErrors(fieldErrors);
        const keys = Object.keys(fieldErrors);
        if (keys.length > 0 && keys.every((k) => k === 'projectId' || k === 'status')) {
          setTopError(Object.values(fieldErrors)[0] ?? 'Invalid value');
        }
      } else if (error instanceof ApiError && error.status === 409) {
        notifications.show({
          color: 'yellow',
          title: 'This risk was changed by someone else',
          message: 'The list now shows the latest version. Open it again to make your changes.',
        });
        onClose();
      } else {
        notifications.show({
          color: 'red',
          title: 'Could not close the risk',
          message: error instanceof Error ? error.message : 'Unexpected error',
        });
      }
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <Modal opened onClose={onClose} title="Close risk">
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <Text fw={700}>{risk.title}</Text>
          <Textarea
            label="Why is this risk being closed?"
            withAsterisk
            autosize
            minRows={3}
            maxLength={2000}
            {...form.getInputProps('note')}
          />
          <Group>
            <Button type="submit" color="red" loading={submitting}>
              Close risk
            </Button>
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

export function CloseRiskModal({ projectId, risk, onClose }: Props) {
  if (risk === undefined) return null;
  return <CloseRiskModalInner key={`${risk.id}-${risk.version}`} projectId={projectId} risk={risk} onClose={onClose} />;
}
