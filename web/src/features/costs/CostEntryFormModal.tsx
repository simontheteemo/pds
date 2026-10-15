import { Alert, Button, Group, Modal, NumberInput, Select, SimpleGrid, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { useConfig } from '../../shared/config';
import { numberInputParts } from '../../shared/format';
import { zodValidate } from '../../shared/validation';
import { useCreateActualCost, useCreateCommitment, useUpdateActualCost, useUpdateCommitment } from './api';
import {
  costEntrySchema,
  emptyCostEntryValues,
  fromEntry,
  toActualCostInput,
  toCommitmentInput,
  type CostEntryFormValues,
  type CostEntryKind,
} from './costEntryFormModel';
import { handleMutationError } from '../../shared/mutationError';
import {
  actualCostTypeLabel,
  actualCostTypes,
  commitmentTypeLabel,
  commitmentTypes,
  type CostReportActual,
  type CostReportCommitment,
  type CostReportContract,
  type CostReportLine,
} from './types';

export type CostEntry = CostReportCommitment['entry'] | CostReportActual['entry'];

type Props = {
  projectId: string;
  kind: CostEntryKind;
  lines: CostReportLine[];
  contracts: CostReportContract[];
  /** The entry to edit, 'new' to add one, or undefined when closed. */
  entry: CostEntry | 'new' | undefined;
  /** Budget line preselected for a new entry. */
  defaultLineId: string | null;
  onClose: () => void;
};

function CostEntryFormModalInner({
  projectId,
  kind,
  lines,
  contracts,
  entry,
  defaultLineId,
  onClose,
}: Props & { entry: CostEntry | 'new' }) {
  const { currency, culture } = useConfig();
  const { currencySymbol, groupSeparator, decimalSeparator } = numberInputParts(culture, currency);
  const createCommitment = useCreateCommitment(projectId);
  const updateCommitment = useUpdateCommitment(projectId);
  const createActual = useCreateActualCost(projectId);
  const updateActual = useUpdateActualCost(projectId);
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const isNew = entry === 'new';
  const noun = kind === 'commitment' ? 'commitment' : 'actual';
  const form = useForm<CostEntryFormValues>({
    mode: 'controlled',
    initialValues: isNew ? emptyCostEntryValues(defaultLineId) : fromEntry(entry),
    validate: zodValidate<CostEntryFormValues>(costEntrySchema(kind)),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      if (kind === 'commitment') {
        if (isNew) await createCommitment.mutateAsync(toCommitmentInput(values));
        else
          await updateCommitment.mutateAsync({
            entryId: entry.id,
            body: { ...toCommitmentInput(values), version: entry.version },
          });
      } else if (isNew) {
        await createActual.mutateAsync(toActualCostInput(values));
      } else {
        await updateActual.mutateAsync({
          entryId: entry.id,
          body: { ...toActualCostInput(values), version: entry.version },
        });
      }
      notifications.show({ color: 'green', message: `${kind === 'commitment' ? 'Commitment' : 'Actual'} saved` });
      onClose();
    } catch (error) {
      handleMutationError(error, {
        entity: noun,
        form,
        setTopError,
        onClose,
        failureTitle: `Could not save the ${noun}`,
        fieldNames: Object.keys(form.values),
      });
    } finally {
      setSubmitting(false);
    }
  });

  const onContractChange = (contractId: string | null) => {
    form.setFieldValue('contractId', contractId);
    const contract = contracts.find((c) => c.id === contractId);
    if (!contract) return;
    if (form.values.description.trim() === '') form.setFieldValue('description', contract.title);
    if (
      kind === 'commitment' &&
      form.values.type === 'Contract' &&
      form.values.amount === '' &&
      contract.valueAmount != null
    ) {
      form.setFieldValue('amount', contract.valueAmount);
    }
  };

  const typeData =
    kind === 'commitment'
      ? commitmentTypes.map((t) => ({ value: t, label: commitmentTypeLabel[t] }))
      : actualCostTypes.map((t) => ({ value: t, label: actualCostTypeLabel[t] }));

  return (
    <Modal opened onClose={onClose} title={`${isNew ? 'Add' : 'Edit'} ${noun}`} size="lg">
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <Select
              label="Budget line"
              withAsterisk
              data={lines.map(({ line }) => ({ value: line.id, label: line.name }))}
              {...form.getInputProps('budgetLineId')}
            />
            <Select label="Type" withAsterisk data={typeData} {...form.getInputProps('type')} />
          </SimpleGrid>
          <Select
            label="Contract"
            clearable
            searchable
            placeholder="None"
            data={contracts.map((c) => ({ value: c.id, label: `${c.contractorName} — ${c.title}` }))}
            {...form.getInputProps('contractId')}
            onChange={onContractChange}
          />
          {kind === 'actual' && <TextInput label="Payee" {...form.getInputProps('payee')} />}
          <TextInput label="Description" withAsterisk {...form.getInputProps('description')} />
          <SimpleGrid cols={{ base: 1, sm: 3 }}>
            <TextInput label="Reference" {...form.getInputProps('reference')} />
            <TextInput label="Date" type="date" withAsterisk {...form.getInputProps('date')} />
            <NumberInput
              label={`Amount (${currency})`}
              withAsterisk
              prefix={currencySymbol}
              thousandSeparator={groupSeparator}
              decimalSeparator={decimalSeparator}
              decimalScale={2}
              allowNegative
              {...form.getInputProps('amount')}
            />
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

export function CostEntryFormModal(props: Props) {
  const { entry } = props;
  if (entry === undefined) return null;
  const key = entry === 'new' ? 'new' : `${entry.id}-${entry.version}`;
  return <CostEntryFormModalInner key={key} {...props} entry={entry} />;
}
