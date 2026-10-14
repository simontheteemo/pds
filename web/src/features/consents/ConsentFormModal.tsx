import { Alert, Button, Group, Modal, Select, SimpleGrid, Stack, Textarea, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { zodValidate } from '../../shared/validation';
import { useCreateConsent, useUpdateConsent } from './api';
import { handleConsentMutationError } from './consentMutationError';
import {
  consentSchema,
  emptyConsentValues,
  fromConsent,
  toConsentInput,
  type ConsentFormValues,
} from './consentFormModel';
import {
  consentStatuses,
  consentStatusLabel,
  consentTypeLabel,
  consentTypes,
  type ConsentDetails,
} from './types';

const typeOptions = consentTypes.map((value) => ({ value, label: consentTypeLabel[value] }));
const statusOptions = consentStatuses.map((value) => ({ value, label: consentStatusLabel[value] }));

type Props = {
  projectId: string;
  consent: ConsentDetails | 'new' | undefined;
  onClose: () => void;
};

function ConsentFormModalInner({ projectId, consent, onClose }: Props & { consent: ConsentDetails | 'new' }) {
  const create = useCreateConsent(projectId);
  const update = useUpdateConsent(projectId);
  const [submitting, setSubmitting] = useState(false);
  const [topError, setTopError] = useState<string | null>(null);
  const isNew = consent === 'new';
  const form = useForm<ConsentFormValues>({
    mode: 'controlled',
    initialValues: isNew ? emptyConsentValues : fromConsent(consent),
    validate: zodValidate<ConsentFormValues>(consentSchema),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setSubmitting(true);
    setTopError(null);
    try {
      if (isNew) {
        await create.mutateAsync(toConsentInput(values));
      } else {
        await update.mutateAsync({
          consentId: consent.id,
          body: { ...toConsentInput(values), version: consent.version },
        });
      }
      notifications.show({ color: 'green', message: 'Consent saved' });
      onClose();
    } catch (error) {
      handleConsentMutationError(error, {
        form,
        setTopError,
        onClose,
        failureTitle: 'Could not save the consent',
        fieldNames: Object.keys(form.values),
        entity: 'consent',
      });
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <Modal opened onClose={onClose} title={isNew ? 'Add consent' : 'Edit consent'} size="lg">
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <Select label="Type" withAsterisk data={typeOptions} {...form.getInputProps('type')} />
          <TextInput label="Title" withAsterisk {...form.getInputProps('title')} />
          <TextInput
            label="Authority"
            placeholder="e.g. district or city council"
            {...form.getInputProps('authority')}
          />
          <TextInput label="Reference" {...form.getInputProps('reference')} />
          <Select
            label="Status"
            withAsterisk
            allowDeselect={false}
            data={statusOptions}
            {...form.getInputProps('status')}
          />
          <SimpleGrid cols={{ base: 1, sm: 3 }}>
            <TextInput type="date" label="Lodged" {...form.getInputProps('lodgedDate')} />
            <TextInput type="date" label="Granted or issued" {...form.getInputProps('grantedDate')} />
            <TextInput type="date" label="Lapse or expiry" {...form.getInputProps('expiryDate')} />
          </SimpleGrid>
          <Textarea label="Notes" maxLength={4000} autosize minRows={3} {...form.getInputProps('notes')} />
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

export function ConsentFormModal({ projectId, consent, onClose }: Props) {
  if (consent === undefined) return null;
  const key = consent === 'new' ? 'new' : `${consent.id}-${consent.version}`;
  return <ConsentFormModalInner key={key} projectId={projectId} consent={consent} onClose={onClose} />;
}
