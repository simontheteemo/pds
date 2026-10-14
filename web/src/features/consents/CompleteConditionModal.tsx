import { Alert, Button, Group, Modal, Stack, Text, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { zodValidate } from '../../shared/validation';
import { useCompleteCondition } from './api';
import { completeSchema, localToday } from './conditionFormModel';
import { handleConsentMutationError } from './consentMutationError';
import type { ConditionDetails } from './types';

type CompleteFormValues = { completedOn: string };

type Props = {
  projectId: string;
  consentId: string;
  condition: ConditionDetails | undefined;
  onClose: () => void;
};

function CompleteConditionModalInner({
  projectId,
  consentId,
  condition,
  onClose,
}: Props & { condition: ConditionDetails }) {
  const complete = useCompleteCondition(projectId);
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const form = useForm<CompleteFormValues>({
    mode: 'controlled',
    initialValues: { completedOn: localToday() },
    validate: zodValidate<CompleteFormValues>(completeSchema),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      await complete.mutateAsync({
        consentId,
        conditionId: condition.id,
        version: condition.version,
        completedOn: values.completedOn,
      });
      notifications.show({ color: 'green', message: 'Condition completed' });
      onClose();
    } catch (error) {
      handleConsentMutationError(error, {
        form,
        setTopError,
        onClose,
        failureTitle: 'Could not complete the condition',
        fieldNames: Object.keys(form.values),
        entity: 'condition',
      });
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <Modal opened onClose={onClose} title="Complete condition">
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <Text size="sm" lineClamp={3} style={{ whiteSpace: 'pre-wrap' }}>
            {condition.description}
          </Text>
          <TextInput
            type="date"
            label="Completed on"
            withAsterisk
            max={localToday()}
            {...form.getInputProps('completedOn')}
          />
          <Group>
            <Button type="submit" loading={submitting}>
              Complete
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

export function CompleteConditionModal({ projectId, consentId, condition, onClose }: Props) {
  if (condition === undefined) return null;
  return (
    <CompleteConditionModalInner
      key={`${condition.id}-${condition.version}`}
      projectId={projectId}
      consentId={consentId}
      condition={condition}
      onClose={onClose}
    />
  );
}
