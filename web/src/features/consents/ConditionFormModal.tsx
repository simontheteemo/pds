import { Alert, Button, Group, Modal, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { zodValidate } from '../../shared/validation';
import { useCreateCondition, useUpdateCondition } from './api';
import {
  conditionSchema,
  emptyConditionValues,
  fromCondition,
  toConditionInput,
  type ConditionFormValues,
} from './conditionFormModel';
import { handleMutationError } from '../../shared/mutationError';
import type { ConditionDetails } from './types';

type Props = {
  projectId: string;
  consentId: string;
  condition: ConditionDetails | 'new' | undefined;
  onClose: () => void;
};

function ConditionFormModalInner({
  projectId,
  consentId,
  condition,
  onClose,
}: Props & { condition: ConditionDetails | 'new' }) {
  const create = useCreateCondition(projectId);
  const update = useUpdateCondition(projectId);
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const isNew = condition === 'new';
  const form = useForm<ConditionFormValues>({
    mode: 'controlled',
    initialValues: isNew ? emptyConditionValues : fromCondition(condition),
    validate: zodValidate<ConditionFormValues>(conditionSchema),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      if (isNew) {
        await create.mutateAsync({ consentId, body: toConditionInput(values) });
      } else {
        await update.mutateAsync({
          consentId,
          conditionId: condition.id,
          body: { ...toConditionInput(values), version: condition.version },
        });
      }
      notifications.show({ color: 'green', message: 'Condition saved' });
      onClose();
    } catch (error) {
      handleMutationError(error, {
        form,
        setTopError,
        onClose,
        failureTitle: 'Could not save the condition',
        fieldNames: Object.keys(form.values),
        entity: 'condition',
      });
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <Modal opened onClose={onClose} title={isNew ? 'Add condition' : 'Edit condition'}>
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <TextInput label="Reference" placeholder="e.g. Condition 12" {...form.getInputProps('reference')} />
          <Textarea
            label="Description"
            withAsterisk
            autosize
            minRows={3}
            maxLength={2000}
            {...form.getInputProps('description')}
          />
          <TextInput type="date" label="Due date" {...form.getInputProps('dueDate')} />
          <Group>
            <Button type="submit" loading={submitting}>
              Save
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

export function ConditionFormModal({ projectId, consentId, condition, onClose }: Props) {
  if (condition === undefined) return null;
  const key = condition === 'new' ? 'new' : `${condition.id}-${condition.version}`;
  return (
    <ConditionFormModalInner
      key={key}
      projectId={projectId}
      consentId={consentId}
      condition={condition}
      onClose={onClose}
    />
  );
}
