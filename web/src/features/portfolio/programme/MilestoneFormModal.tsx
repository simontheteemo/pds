import { Alert, Button, Group, Modal, NumberInput, Select, SimpleGrid, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { handleMutationError } from '../../../shared/mutationError';
import { zodValidate } from '../../../shared/validation';
import { useCreateMilestone, useUpdateMilestone } from './api';
import {
  emptyMilestoneValues,
  fromMilestone,
  milestoneSchema,
  toMilestoneInput,
  type MilestoneFormValues,
} from './milestoneFormModel';
import { milestoneCategories, milestoneCategoryLabel, type MilestoneDetails } from './types';

type Props = {
  projectId: string;
  /** All current milestones, used to default the sort order of a new one. */
  milestones: MilestoneDetails[];
  milestone: MilestoneDetails | 'new' | undefined;
  onClose: () => void;
};

const categoryOptions = milestoneCategories.map((c) => ({ value: c, label: milestoneCategoryLabel[c] }));

function MilestoneFormModalInner({
  projectId,
  milestones,
  milestone,
  onClose,
}: Props & { milestone: MilestoneDetails | 'new' }) {
  const create = useCreateMilestone(projectId);
  const update = useUpdateMilestone(projectId);
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const isNew = milestone === 'new';
  const form = useForm<MilestoneFormValues>({
    mode: 'controlled',
    initialValues: isNew ? emptyMilestoneValues(milestones) : fromMilestone(milestone),
    validate: zodValidate<MilestoneFormValues>(milestoneSchema),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      if (isNew) {
        await create.mutateAsync(toMilestoneInput(values));
      } else {
        await update.mutateAsync({
          milestoneId: milestone.id,
          body: { ...toMilestoneInput(values), version: milestone.version },
        });
      }
      notifications.show({ color: 'green', message: 'Milestone saved' });
      onClose();
    } catch (error) {
      handleMutationError(error, {
        entity: 'milestone',
        form,
        setTopError,
        onClose,
        failureTitle: 'Could not save the milestone',
        fieldNames: Object.keys(form.values),
      });
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <Modal opened onClose={onClose} title={isNew ? 'Add milestone' : 'Edit milestone'} size="lg">
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <TextInput label="Name" withAsterisk {...form.getInputProps('name')} />
          <Select label="Category" withAsterisk data={categoryOptions} {...form.getInputProps('category')} />
          <SimpleGrid cols={{ base: 1, sm: 3 }}>
            <TextInput label="Planned" type="date" {...form.getInputProps('plannedDate')} />
            <TextInput
              label="Forecast"
              type="date"
              description="Leave blank if still on plan"
              {...form.getInputProps('forecastDate')}
            />
            <TextInput label="Actual" type="date" {...form.getInputProps('actualDate')} />
          </SimpleGrid>
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <TextInput label="Owner" {...form.getInputProps('owner')} />
            <NumberInput label="Sort order" withAsterisk min={0} max={9999} allowDecimal={false} {...form.getInputProps('sortOrder')} />
          </SimpleGrid>
          <Textarea label="Notes" autosize minRows={3} {...form.getInputProps('notes')} />
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

export function MilestoneFormModal({ projectId, milestones, milestone, onClose }: Props) {
  if (milestone === undefined) return null;
  const key = milestone === 'new' ? 'new' : `${milestone.id}-${milestone.version}`;
  return (
    <MilestoneFormModalInner key={key} projectId={projectId} milestones={milestones} milestone={milestone} onClose={onClose} />
  );
}
