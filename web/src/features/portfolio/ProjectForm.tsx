import { Button, Fieldset, Group, NumberInput, Select, SimpleGrid, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useState } from 'react';
import { Link } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { numberInputParts } from '../../shared/format';
import { serverFieldErrors, zodValidate } from '../../shared/validation';
import { projectSchema, type ProjectFormValues } from './projectFormModel';
import { stageLabel, stages, statuses, statusLabel } from './types';

type Props = {
  initialValues: ProjectFormValues;
  submitLabel: string;
  cancelTo: string;
  onSubmit: (values: ProjectFormValues) => Promise<void>;
  onError: (error: unknown) => void;
};

const stageOptions = stages.map((value) => ({ value, label: stageLabel[value] }));
const statusOptions = statuses.map((value) => ({ value, label: statusLabel[value] }));

export function ProjectForm({ initialValues, submitLabel, cancelTo, onSubmit, onError }: Props) {
  const { currency, culture } = useConfig();
  const { currencySymbol, groupSeparator, decimalSeparator } = numberInputParts(culture, currency);
  const [submitting, setSubmitting] = useState(false);
  const form = useForm<ProjectFormValues>({
    mode: 'controlled',
    initialValues,
    validate: zodValidate<ProjectFormValues>(projectSchema),
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
        <Fieldset legend="Project">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <TextInput label="Code" description="Up to 20 letters, digits or hyphens" withAsterisk {...form.getInputProps('code')} />
            <TextInput label="Name" withAsterisk {...form.getInputProps('name')} />
            <Select label="Stage" withAsterisk data={stageOptions} {...form.getInputProps('stage')} />
            <Select label="Status" withAsterisk data={statusOptions} {...form.getInputProps('status')} />
          </SimpleGrid>
        </Fieldset>

        <Fieldset legend="Site">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <TextInput label="Address" withAsterisk {...form.getInputProps('site.addressLine')} />
            <TextInput label="Suburb" {...form.getInputProps('site.suburb')} />
            <TextInput label="City" withAsterisk {...form.getInputProps('site.city')} />
            <TextInput label="Region" {...form.getInputProps('site.region')} />
            <TextInput label="Postcode" {...form.getInputProps('site.postcode')} />
            <NumberInput
              label="Land area"
              suffix=" m²"
              min={0}
              thousandSeparator={groupSeparator}
              decimalSeparator={decimalSeparator}
              {...form.getInputProps('site.landAreaSqm')}
            />
            <TextInput label="Legal description" {...form.getInputProps('site.legalDescription')} />
            <TextInput label="Title reference" {...form.getInputProps('site.titleReference')} />
          </SimpleGrid>
        </Fieldset>

        <Fieldset legend="Timeline">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <TextInput type="date" label="Planned start" {...form.getInputProps('plannedStart')} />
            <TextInput type="date" label="Planned completion" {...form.getInputProps('plannedCompletion')} />
            <TextInput type="date" label="Actual start" {...form.getInputProps('actualStart')} />
            <TextInput type="date" label="Actual completion" {...form.getInputProps('actualCompletion')} />
          </SimpleGrid>
        </Fieldset>

        <Fieldset legend="Budget and people">
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <NumberInput
              label={`Budget (${currency})`}
              prefix={currencySymbol}
              min={0}
              decimalScale={2}
              thousandSeparator={groupSeparator}
              decimalSeparator={decimalSeparator}
              {...form.getInputProps('budgetAmount')}
            />
            <TextInput label="Project manager" {...form.getInputProps('projectManager')} />
          </SimpleGrid>
        </Fieldset>

        <Textarea label="Description" autosize minRows={3} {...form.getInputProps('description')} />

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
