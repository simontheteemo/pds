import { Alert, Button, FileInput, Group, Modal, Stack } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { useConfig } from '../../shared/config';
import { zodValidate } from '../../shared/validation';
import { useUploadDocument } from './api';
import {
  documentSchema,
  emptyDocumentValues,
  titleFromFileName,
  toMetadataInput,
  type DocumentFormValues,
} from './documentFormModel';
import { DocumentMetadataFields } from './DocumentMetadataFields';
import { formatBytes, validateFile } from './format';
import { LinkedRecordSelect } from './LinkedRecordSelect';
import { handleUploadError, LINK_FIELD_NAMES } from './uploadError';
import type { DocumentSettings } from './types';

type Props = { projectId: string; opened: boolean; onClose: () => void; settings: DocumentSettings };

function UploadDocumentModalInner({ projectId, onClose, settings }: Omit<Props, 'opened'>) {
  const config = useConfig();
  const upload = useUploadDocument(projectId);
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [topError, setTopError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const form = useForm<DocumentFormValues>({
    mode: 'controlled',
    initialValues: emptyDocumentValues,
    validate: zodValidate<DocumentFormValues>(documentSchema),
  });

  const onFileChange = (next: File | null) => {
    setFile(next);
    setFileError(null);
    if (next && form.values.title.trim() === '') form.setFieldValue('title', titleFromFileName(next.name));
  };

  const handleSubmit = form.onSubmit(async (values) => {
    setTopError(null);
    const problem = validateFile(file, settings, config.culture);
    setFileError(problem);
    if (problem || !file) return;
    setSubmitting(true);
    try {
      await upload.mutateAsync({ file, metadata: toMetadataInput(values) });
      notifications.show({ color: 'green', message: 'Document uploaded' });
      onClose();
    } catch (error) {
      handleUploadError(error, {
        form,
        fieldNames: [...Object.keys(form.values), ...LINK_FIELD_NAMES],
        setFileError,
        setTopError,
        onClose,
        failureTitle: 'Could not upload the document',
      });
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <Modal
      opened
      onClose={onClose}
      title="Upload document"
      size="lg"
      closeOnClickOutside={!submitting}
      closeOnEscape={!submitting}
      withCloseButton={!submitting}
    >
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <FileInput
            label="File"
            withAsterisk
            description={`Up to ${formatBytes(settings.maxFileSizeBytes, config.culture)}. Allowed: ${settings.allowedExtensions.join(', ')}`}
            accept={settings.allowedExtensions.map((e) => (e.startsWith('.') ? e : `.${e}`)).join(',')}
            value={file}
            onChange={onFileChange}
            error={fileError}
            clearable
          />
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
              {submitting ? 'Uploading…' : 'Upload'}
            </Button>
            <Button variant="default" onClick={onClose} disabled={submitting}>
              Cancel
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

export function UploadDocumentModal({ opened, ...rest }: Props) {
  if (!opened) return null;
  return <UploadDocumentModalInner {...rest} />;
}
