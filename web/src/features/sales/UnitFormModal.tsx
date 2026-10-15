import { Alert, Button, Group, Modal, NumberInput, Select, SimpleGrid, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { useConfig } from '../../shared/config';
import { numberInputParts } from '../../shared/format';
import { zodValidate } from '../../shared/validation';
import { useCreateUnit, useUpdateUnit } from './api';
import { handleMutationError } from '../../shared/mutationError';
import { emptyUnitValues, fromUnit, toUnitInput, unitSchema, type UnitFormValues } from './unitFormModel';
import { unitTypeLabel, unitTypes, type UnitDetails } from './types';

const typeOptions = unitTypes.map((value) => ({ value, label: unitTypeLabel[value] }));

type Props = {
  projectId: string;
  unit: UnitDetails | 'new' | undefined;
  onClose: () => void;
};

function UnitFormModalInner({ projectId, unit, onClose }: Props & { unit: UnitDetails | 'new' }) {
  const { currency, culture } = useConfig();
  const { currencySymbol, groupSeparator, decimalSeparator } = numberInputParts(culture, currency);
  const create = useCreateUnit(projectId);
  const update = useUpdateUnit(projectId);
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const isNew = unit === 'new';
  const form = useForm<UnitFormValues>({
    mode: 'controlled',
    initialValues: isNew ? emptyUnitValues : fromUnit(unit),
    validate: zodValidate<UnitFormValues>(unitSchema),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      if (isNew) {
        await create.mutateAsync(toUnitInput(values));
      } else {
        await update.mutateAsync({ unitId: unit.id, body: { ...toUnitInput(values), version: unit.version } });
      }
      notifications.show({ color: 'green', message: 'Unit saved' });
      onClose();
    } catch (error) {
      handleMutationError(error, {
        entity: 'unit',
        form,
        setTopError,
        onClose,
        failureTitle: 'Could not save the unit',
        fieldNames: Object.keys(form.values),
      });
    } finally {
      setSubmitting(false);
    }
  });

  const roomProps = { allowDecimal: false, allowNegative: false, min: 0, max: 20 } as const;
  const areaProps = {
    suffix: ' m²',
    min: 0,
    allowNegative: false,
    decimalScale: 2,
    thousandSeparator: groupSeparator,
    decimalSeparator,
  } as const;

  return (
    <Modal opened onClose={onClose} title={isNew ? 'Add unit' : 'Edit unit'} size="lg">
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <TextInput
            label="Identifier"
            placeholder="e.g. Lot 12 or A-301"
            withAsterisk
            maxLength={50}
            {...form.getInputProps('identifier')}
          />
          <Select label="Type" withAsterisk data={typeOptions} {...form.getInputProps('type')} />
          <SimpleGrid cols={3}>
            <NumberInput label="Bedrooms" {...roomProps} {...form.getInputProps('bedrooms')} />
            <NumberInput label="Bathrooms" {...roomProps} {...form.getInputProps('bathrooms')} />
            <NumberInput label="Car parks" {...roomProps} {...form.getInputProps('carParks')} />
          </SimpleGrid>
          <SimpleGrid cols={{ base: 1, sm: 2 }}>
            <NumberInput label="Floor area" {...areaProps} {...form.getInputProps('floorAreaSqm')} />
            <NumberInput label="Land area" {...areaProps} {...form.getInputProps('landAreaSqm')} />
          </SimpleGrid>
          <NumberInput
            label={`List price (${currency})`}
            prefix={currencySymbol}
            min={0}
            allowNegative={false}
            decimalScale={2}
            thousandSeparator={groupSeparator}
            decimalSeparator={decimalSeparator}
            {...form.getInputProps('listPriceAmount')}
          />
          <Textarea label="Notes" autosize minRows={3} maxLength={4000} {...form.getInputProps('notes')} />
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

export function UnitFormModal({ projectId, unit, onClose }: Props) {
  if (unit === undefined) return null;
  const key = unit === 'new' ? 'new' : `${unit.id}-${unit.version}`;
  return <UnitFormModalInner key={key} projectId={projectId} unit={unit} onClose={onClose} />;
}
