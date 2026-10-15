import { Button, Fieldset, Group, Select, SimpleGrid, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useState } from 'react';
import { Link } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { serverFieldErrors, zodValidate } from '../../shared/validation';
import { contractorSchema, type ContractorFormValues } from './contractorFormModel';
import { contractorTypeLabel, contractorTypes } from './types';

type Props = {
  initialValues: ContractorFormValues;
  submitLabel: string;
  cancelTo: string;
  onSubmit: (values: ContractorFormValues) => Promise<void>;
  onError: (error: unknown) => void;
};

const typeOptions = contractorTypes.map((value) => ({ value, label: contractorTypeLabel[value] }));

export function ContractorForm({ initialValues, submitLabel, cancelTo, onSubmit, onError }: Props) {
  const [submitting, setSubmitting] = useState(false);
  const form = useForm<ContractorFormValues>({
    mode: 'controlled',
    initialValues,
    validate: zodValidate<ContractorFormValues>(contractorSchema),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    try {
      await onSubmit(values);
    } catch (error) {
      if (error instanceof ApiError && error.status === 400 && error.problem?.errors) {
        form.setErrors(serverFieldErrors(error.problem));
      } else {
        onError(error);
      }
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <form onSubmit={handleSubmit} noValidate>
      <Stack gap="lg" maw={880}>
        <Fieldset legend="Contractor">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <TextInput label="Name" withAsterisk {...form.getInputProps('name')} />
            <Select label="Type" withAsterisk data={typeOptions} {...form.getInputProps('type')} />
            <TextInput label="Trade" description="For example Civil, Electrical or Structural engineer" {...form.getInputProps('trade')} />
            <TextInput label="NZBN" description="13 digits" {...form.getInputProps('nzbn')} />
          </SimpleGrid>
        </Fieldset>

        <Fieldset legend="Contact">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <TextInput label="Contact name" {...form.getInputProps('contactName')} />
            <TextInput label="Email" type="email" {...form.getInputProps('email')} />
            <TextInput label="Phone" {...form.getInputProps('phone')} />
            <TextInput label="Address" {...form.getInputProps('address')} />
          </SimpleGrid>
        </Fieldset>

        <Fieldset legend="Compliance">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <TextInput type="date" label="Public liability expiry" {...form.getInputProps('publicLiabilityExpiry')} />
            <TextInput type="date" label="Professional indemnity expiry" {...form.getInputProps('professionalIndemnityExpiry')} />
            <TextInput label="Licence number" {...form.getInputProps('licenceNumber')} />
            <TextInput type="date" label="Licence expiry" {...form.getInputProps('licenceExpiry')} />
          </SimpleGrid>
        </Fieldset>

        <Fieldset legend="Notes">
          <Textarea aria-label="Notes" autosize minRows={3} {...form.getInputProps('notes')} />
        </Fieldset>

        <Group>
          <Button type="submit" loading={submitting}>
            {submitLabel}
          </Button>
          <Button component={Link} to={cancelTo} variant="default">
            Cancel
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
