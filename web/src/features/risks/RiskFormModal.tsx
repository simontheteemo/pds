import { Alert, Button, Group, Modal, SegmentedControl, Select, SimpleGrid, Stack, Text, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { ApiError } from '../../shared/api/client';
import { serverFieldErrors, zodValidate } from '../../shared/validation';
import { useCreateRisk, useUpdateRisk } from './api';
import { emptyRiskValues, fromRisk, riskSchema, toRiskInput, type RiskFormValues } from './riskFormModel';
import { RiskBandBadge } from './RiskBandBadge';
import { bandFor, categories, categoryLabel, impactLabels, likelihoodLabels, type RiskDetails } from './types';

const categoryOptions = categories.map((value) => ({ value, label: categoryLabel[value] }));
const likelihoodOptions = likelihoodLabels.map((label, i) => ({ value: String(i + 1), label: `${i + 1} – ${label}` }));
const impactOptions = impactLabels.map((label, i) => ({ value: String(i + 1), label: `${i + 1} – ${label}` }));

type Props = {
  projectId: string;
  risk: RiskDetails | 'new' | undefined;
  onClose: () => void;
};

function RiskFormModalInner({ projectId, risk, onClose }: Props & { risk: RiskDetails | 'new' }) {
  const create = useCreateRisk(projectId);
  const update = useUpdateRisk(projectId);
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const isNew = risk === 'new';
  const form = useForm<RiskFormValues>({
    mode: 'controlled',
    initialValues: isNew ? emptyRiskValues : fromRisk(risk),
    validate: zodValidate<RiskFormValues>(riskSchema),
  });

  const likelihoodNum = form.values.likelihood === null ? null : Number(form.values.likelihood);
  const impactNum = form.values.impact === null ? null : Number(form.values.impact);

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      if (isNew) {
        await create.mutateAsync(toRiskInput(values));
      } else {
        await update.mutateAsync({
          riskId: risk.id,
          body: { ...toRiskInput(values), status: values.status, version: risk.version },
        });
      }
      notifications.show({ color: 'green', message: 'Risk saved' });
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
          title: 'Could not save the risk',
          message: error instanceof Error ? error.message : 'Unexpected error',
        });
      }
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <Modal opened onClose={onClose} title={isNew ? 'Add risk' : 'Edit risk'} size="lg">
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <TextInput label="Title" withAsterisk {...form.getInputProps('title')} />
          <Select label="Category" withAsterisk data={categoryOptions} {...form.getInputProps('category')} />
          <SimpleGrid cols={2}>
            <Select label="Likelihood" withAsterisk data={likelihoodOptions} {...form.getInputProps('likelihood')} />
            <Select label="Impact" withAsterisk data={impactOptions} {...form.getInputProps('impact')} />
          </SimpleGrid>
          {likelihoodNum && impactNum ? (
            <RiskBandBadge band={bandFor(likelihoodNum * impactNum)} score={likelihoodNum * impactNum} />
          ) : (
            <Text c="dimmed" size="sm">
              Choose likelihood and impact
            </Text>
          )}
          <TextInput label="Owner" {...form.getInputProps('owner')} />
          <TextInput type="date" label="Due date" {...form.getInputProps('dueDate')} />
          {!isNew && (
            <SegmentedControl
              data={[
                { value: 'Open', label: 'Open' },
                { value: 'Mitigating', label: 'Mitigating' },
              ]}
              {...form.getInputProps('status')}
            />
          )}
          <Textarea label="Description" autosize minRows={3} {...form.getInputProps('description')} />
          <Textarea label="Mitigation" autosize minRows={3} {...form.getInputProps('mitigation')} />
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

export function RiskFormModal({ projectId, risk, onClose }: Props) {
  if (risk === undefined) return null;
  const key = risk === 'new' ? 'new' : `${risk.id}-${risk.version}`;
  return <RiskFormModalInner key={key} projectId={projectId} risk={risk} onClose={onClose} />;
}
