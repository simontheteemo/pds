import { Alert, Button, Group, Modal, NumberInput, SimpleGrid, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { useConfig } from '../../shared/config';
import { numberInputParts } from '../../shared/format';
import { zodValidate } from '../../shared/validation';
import { useCreateBudgetLine, useUpdateBudgetLine } from './api';
import {
  budgetLineSchema,
  emptyBudgetLineValues,
  fromBudgetLine,
  toBudgetLineInput,
  type BudgetLineFormValues,
} from './budgetLineFormModel';
import { handleMutationError } from '../../shared/mutationError';
import type { CostReportLine } from './types';

type Props = {
  projectId: string;
  /** All current report lines, used to default the sort order of a new line. */
  lines: CostReportLine[];
  line: CostReportLine['line'] | 'new' | undefined;
  onClose: () => void;
};

function BudgetLineFormModalInner({ projectId, lines, line, onClose }: Props & { line: CostReportLine['line'] | 'new' }) {
  const { currency, culture } = useConfig();
  const { currencySymbol, groupSeparator, decimalSeparator } = numberInputParts(culture, currency);
  const create = useCreateBudgetLine(projectId);
  const update = useUpdateBudgetLine(projectId);
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const isNew = line === 'new';
  const form = useForm<BudgetLineFormValues>({
    mode: 'controlled',
    initialValues: isNew ? emptyBudgetLineValues(lines) : fromBudgetLine(line),
    validate: zodValidate<BudgetLineFormValues>(budgetLineSchema),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      if (isNew) {
        await create.mutateAsync(toBudgetLineInput(values));
      } else {
        await update.mutateAsync({ lineId: line.id, body: { ...toBudgetLineInput(values), version: line.version } });
      }
      notifications.show({ color: 'green', message: 'Budget line saved' });
      onClose();
    } catch (error) {
      handleMutationError(error, {
        entity: 'budget line',
        form,
        setTopError,
        onClose,
        failureTitle: 'Could not save the budget line',
        fieldNames: Object.keys(form.values),
      });
    } finally {
      setSubmitting(false);
    }
  });

  const money = { prefix: currencySymbol, thousandSeparator: groupSeparator, decimalSeparator, decimalScale: 2, min: 0 };

  return (
    <Modal opened onClose={onClose} title={isNew ? 'Add budget line' : 'Edit budget line'} size="lg">
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <TextInput label="Name" withAsterisk {...form.getInputProps('name')} />
          <SimpleGrid cols={2}>
            <NumberInput
              label={`Budget (${currency})`}
              withAsterisk
              {...money}
              {...form.getInputProps('budgetAmount')}
            />
            <NumberInput label="Sort order" withAsterisk min={0} max={9999} allowDecimal={false} {...form.getInputProps('sortOrder')} />
          </SimpleGrid>
          <NumberInput
            label={`Forecast remaining (${currency})`}
            description="Leave blank to assume the rest of the budget will be spent."
            {...money}
            {...form.getInputProps('forecastRemainingAmount')}
          />
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

export function BudgetLineFormModal({ projectId, lines, line, onClose }: Props) {
  if (line === undefined) return null;
  const key = line === 'new' ? 'new' : `${line.id}-${line.version}`;
  return <BudgetLineFormModalInner key={key} projectId={projectId} lines={lines} line={line} onClose={onClose} />;
}
