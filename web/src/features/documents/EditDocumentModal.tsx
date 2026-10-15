import { Alert, Button, Group, Modal, Stack } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { handleMutationError } from '../../shared/mutationError';
import { zodValidate } from '../../shared/validation';
import { useUpdateDocument } from './api';
import { documentSchema, fromDocument, toMetadataInput, type DocumentFormValues } from './documentFormModel';
import { DocumentMetadataFields } from './DocumentMetadataFields';
import { LinkedRecordSelect } from './LinkedRecordSelect';
import type { DocumentDetails, DocumentSettings } from './types';
import { LINK_FIELD_NAMES } from './uploadError';

type Props = {
  projectId: string;
  document: DocumentDetails | undefined;
  settings: DocumentSettings;
  onClose: () => void;
};

function EditForm({ projectId, document, settings, onClose }: Props & { document: DocumentDetails }) {
  const update = useUpdateDocument(projectId);
  const [topError, setTopError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const form = useForm<DocumentFormValues>({
    mode: 'controlled',
    initialValues: fromDocument(document),
    validate: zodValidate<DocumentFormValues>(documentSchema),
  });

  const handleSubmit = form.onSubmit(async (values) => {
    setTopError(null);
    setSubmitting(true);
    try {
      await update.mutateAsync({
        documentId: document.id,
        body: { ...toMetadataInput(values), version: document.version },
      });
      notifications.show({ color: 'green', message: 'Document saved' });
      onClose();
    } catch (error) {
      handleMutationError(error, {
        entity: 'document',
        form,
        setTopError,
        onClose,
        failureTitle: 'Could not save the document',
        fieldNames: [...Object.keys(form.values), ...LINK_FIELD_NAMES],
      });
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <form onSubmit={handleSubmit} noValidate>
      <Stack gap="md">
        {topError && <Alert color="red">{topError}</Alert>}
        <DocumentMetadataFields
          form={form}
          categories={settings.categories}
          linkSlot={
            <LinkedRecordSelect
              projectId={projectId}
              value={form.values.link}
              onChange={(v) => form.setFieldValue('link', v)}
              error={
                form.errors['link'] ??
                form.errors['link.label'] ??
                form.errors['link.recordId'] ??
                form.errors['link.module']
              }
            />
          }
        />
        <Group>
          <Button type="submit" loading={submitting}>
            Save
          </Button>
          <Button variant="default" onClick={onClose} disabled={submitting}>
            Cancel
          </Button>
        </Group>
      </Stack>
    </form>
  );
}

export function EditDocumentModal(props: Props) {
  const { document } = props;
  return (
    <Modal opened={document !== undefined} onClose={props.onClose} title="Edit document" size="lg">
      {document && <EditForm key={document.id} {...props} document={document} />}
    </Modal>
  );
}
