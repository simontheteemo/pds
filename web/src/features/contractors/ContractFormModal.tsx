import { Alert, Button, Group, Loader, Modal, NumberInput, Select, SimpleGrid, Stack, Text, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { useConfig } from '../../shared/config';
import { joinLabel, numberInputParts } from '../../shared/format';
import { zodValidate } from '../../shared/validation';
import { useContractors, useCreateContract, useProjectContract, useUpdateContract } from './api';
import { ComplianceBadge } from './ComplianceBadge';
import { handleMutationError } from '../../shared/mutationError';
import {
  contractSchema,
  emptyContractValues,
  fromContract,
  toContractInput,
  type ContractFormValues,
} from './contractFormModel';
import {
  contractFormLabel,
  contractForms,
  contractStatuses,
  contractStatusLabel,
  type ContractDetails,
  type ProjectContract,
} from './types';

const formOptions = contractForms.map((value) => ({ value, label: contractFormLabel[value] }));
const statusOptions = contractStatuses.map((value) => ({ value, label: contractStatusLabel[value] }));

type Props = {
  projectId: string;
  contract: ProjectContract | 'new' | undefined;
  onClose: () => void;
};

function ContractFormBody({
  projectId,
  existing,
  onClose,
}: {
  projectId: string;
  existing: ContractDetails | undefined;
  onClose: () => void;
}) {
  const { currency, culture } = useConfig();
  const { currencySymbol, groupSeparator, decimalSeparator } = numberInputParts(culture, currency);
  const create = useCreateContract(projectId);
  const update = useUpdateContract(projectId);
  const contractors = useContractors({ includeInactive: false });
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const form = useForm<ContractFormValues>({
    mode: 'controlled',
    initialValues: existing ? fromContract(existing) : emptyContractValues,
    validate: zodValidate<ContractFormValues>(contractSchema),
  });

  const contractorOptions = (contractors.data ?? []).map((c) => ({ value: c.id, label: joinLabel(c.name, c.trade) }));
  // When editing, the contractor may be inactive and absent from the list; keep it selectable.
  if (existing && !contractorOptions.some((o) => o.value === existing.contractorId)) {
    contractorOptions.push({ value: existing.contractorId, label: existing.contractorName });
  }
  const selected = contractors.data?.find((c) => c.id === form.values.contractorId);

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      if (existing) {
        await update.mutateAsync({
          contractId: existing.id,
          body: { ...toContractInput(values), version: existing.version },
        });
      } else {
        await create.mutateAsync(toContractInput(values));
      }
      notifications.show({ color: 'green', message: 'Contract saved' });
      onClose();
    } catch (error) {
      handleMutationError(error, {
        entity: 'contract',
        form,
        setTopError,
        onClose,
        failureTitle: 'Could not save the contract',
        fieldNames: Object.keys(form.values).filter((k) => k !== 'contractorId' || !existing),
      });
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <form onSubmit={handleSubmit} noValidate>
      <Stack gap="md">
        {topError && <Alert color="red">{topError}</Alert>}
        <Group align="flex-end" wrap="nowrap">
          <Select
            style={{ flex: 1 }}
            label="Contractor"
            withAsterisk
            searchable
            disabled={!!existing}
            data={contractorOptions}
            nothingFoundMessage="No contractors found"
            {...form.getInputProps('contractorId')}
          />
          {selected && <ComplianceBadge status={selected.compliance} />}
        </Group>
        {selected && selected.compliance !== 'Current' && (
          <Text size="sm" c="dimmed">
            (check insurance)
          </Text>
        )}
        <TextInput label="Title" withAsterisk {...form.getInputProps('title')} />
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <TextInput label="Reference" {...form.getInputProps('reference')} />
          <Select label="Form" withAsterisk data={formOptions} {...form.getInputProps('form')} />
          <NumberInput
            label={`Value (${currency})`}
            prefix={currencySymbol}
            min={0}
            decimalScale={2}
            thousandSeparator={groupSeparator}
            decimalSeparator={decimalSeparator}
            {...form.getInputProps('valueAmount')}
          />
          <NumberInput
            label="Retention"
            suffix=" %"
            min={0}
            max={20}
            decimalScale={1}
            decimalSeparator={decimalSeparator}
            {...form.getInputProps('retentionPercent')}
          />
          <TextInput type="date" label="Start date" {...form.getInputProps('startDate')} />
          <TextInput type="date" label="End date" {...form.getInputProps('endDate')} />
        </SimpleGrid>
        <Select label="Status" allowDeselect={false} data={statusOptions} {...form.getInputProps('status')} />
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
  );
}

function EditLoader({ projectId, contract, onClose }: Props & { contract: ProjectContract }) {
  const details = useProjectContract(projectId, contract.id);
  if (details.error) return <Alert color="red">{details.error.message}</Alert>;
  if (!details.data) return <Loader size="sm" />;
  return (
    <ContractFormBody
      key={`${details.data.id}-${details.data.version}`}
      projectId={projectId}
      existing={details.data}
      onClose={onClose}
    />
  );
}

export function ContractFormModal({ projectId, contract, onClose }: Props) {
  if (contract === undefined) return null;
  const isNew = contract === 'new';
  return (
    <Modal opened onClose={onClose} title={isNew ? 'Add contract' : 'Edit contract'} size="lg">
      {isNew ? (
        <ContractFormBody projectId={projectId} existing={undefined} onClose={onClose} />
      ) : (
        <EditLoader projectId={projectId} contract={contract} onClose={onClose} />
      )}
    </Modal>
  );
}
