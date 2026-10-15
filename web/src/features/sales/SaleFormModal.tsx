import { Alert, Button, Fieldset, Group, Modal, NumberInput, Select, SimpleGrid, Stack, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { useConfig } from '../../shared/config';
import { numberInputParts } from '../../shared/format';
import { zodValidate } from '../../shared/validation';
import { useUpdateSale } from './api';
import { fromUnit, saleSchema, toSaleInput, type SaleFormValues } from './saleFormModel';
import { handleMutationError } from '../../shared/mutationError';
import {
  hasSale,
  requiresAgreement,
  requiresSettlement,
  requiresUnconditional,
  saleStatuses,
  saleStatusLabel,
  type SaleStatus,
  type UnitDetails,
} from './types';

const statusOptions = saleStatuses.map((value) => ({ value, label: saleStatusLabel[value] }));

type Props = {
  projectId: string;
  unit: UnitDetails | undefined;
  onClose: () => void;
};

function SaleFormModalInner({ projectId, unit, onClose }: Props & { unit: UnitDetails }) {
  const { currency, culture } = useConfig();
  const { currencySymbol, groupSeparator, decimalSeparator } = numberInputParts(culture, currency);
  const update = useUpdateSale(projectId);
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const form = useForm<SaleFormValues>({
    mode: 'controlled',
    initialValues: fromUnit(unit),
    validate: zodValidate<SaleFormValues>(saleSchema),
  });

  const status = form.values.status;
  const showSale = hasSale(status);

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      await update.mutateAsync({ unitId: unit.id, body: toSaleInput(values, unit.version) });
      notifications.show({ color: 'green', message: 'Sale updated' });
      onClose();
    } catch (error) {
      handleMutationError(error, {
        entity: 'unit',
        form,
        setTopError,
        onClose,
        failureTitle: 'Could not update the sale',
        fieldNames: Object.keys(form.values),
      });
    } finally {
      setSubmitting(false);
    }
  });

  const moneyProps = {
    prefix: currencySymbol,
    min: 0,
    allowNegative: false,
    decimalScale: 2,
    thousandSeparator: groupSeparator,
    decimalSeparator,
  } as const;
  const statusInput = form.getInputProps('status');

  return (
    <Modal opened onClose={onClose} title={`Sale — ${unit.identifier}`} size="lg">
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <Select
            label="Status"
            withAsterisk
            allowDeselect={false}
            data={statusOptions}
            {...statusInput}
            onChange={(value) => {
              if (value) form.setFieldValue('status', value as SaleStatus);
            }}
          />
          {!showSale && hasSale(unit.status) && (
            <Alert color="yellow">Saving clears the buyer and sale details.</Alert>
          )}
          {showSale && (
            <>
              <Fieldset legend="Buyer">
                <Stack gap="sm">
                  <TextInput label="Name" withAsterisk maxLength={200} {...form.getInputProps('buyerName')} />
                  <SimpleGrid cols={{ base: 1, sm: 2 }}>
                    <TextInput label="Email" type="email" maxLength={200} {...form.getInputProps('buyerEmail')} />
                    <TextInput label="Phone" type="tel" maxLength={50} {...form.getInputProps('buyerPhone')} />
                  </SimpleGrid>
                  <TextInput label="Buyer's solicitor" maxLength={200} {...form.getInputProps('buyerSolicitor')} />
                </Stack>
              </Fieldset>
              <Fieldset legend="Sale">
                <Stack gap="sm">
                  <TextInput
                    type="date"
                    label="S&P agreement date"
                    withAsterisk={requiresAgreement(status)}
                    {...form.getInputProps('agreementDate')}
                  />
                  <SimpleGrid cols={{ base: 1, sm: 2 }}>
                    <NumberInput
                      label={`Sale price (${currency})`}
                      withAsterisk={requiresAgreement(status)}
                      {...moneyProps}
                      {...form.getInputProps('salePriceAmount')}
                    />
                    <NumberInput label={`Deposit (${currency})`} {...moneyProps} {...form.getInputProps('depositAmount')} />
                  </SimpleGrid>
                  <SimpleGrid cols={{ base: 1, sm: 2 }}>
                    <TextInput
                      type="date"
                      label="Unconditional date"
                      withAsterisk={requiresUnconditional(status)}
                      {...form.getInputProps('unconditionalDate')}
                    />
                    <TextInput
                      type="date"
                      label="Settlement date"
                      description="Due date until settled; the actual date once settled."
                      inputWrapperOrder={['label', 'input', 'description', 'error']}
                      withAsterisk={requiresSettlement(status)}
                      {...form.getInputProps('settlementDate')}
                    />
                  </SimpleGrid>
                </Stack>
              </Fieldset>
            </>
          )}
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

export function SaleFormModal({ projectId, unit, onClose }: Props) {
  if (unit === undefined) return null;
  return <SaleFormModalInner key={`${unit.id}-${unit.version}`} projectId={projectId} unit={unit} onClose={onClose} />;
}
