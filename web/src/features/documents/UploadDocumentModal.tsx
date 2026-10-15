import { Alert, Button, FileInput, Group, Modal, Stack } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { handleMutationError } from '../../shared/mutationError';
import { serverFieldErrors, zodValidate } from '../../shared/validation';
import { UploadFailedError, useUploadDocument } from './api';
import { documentSchema, emptyDocumentValues, titleFromFileName, toMetadataInput, type DocumentFormValues } from './documentFormModel';
import { DocumentMetadataFields } from './DocumentMetadataFields';
import { formatBytes } from './format';
import type { DocumentSettings } from './types';

type Props = { projectId: string; opened: boolean; onClose: () => void; settings: DocumentSettings };

const FILE_KEYS = ['fileName', 'sizeBytes', 'uploadId'];

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

  const checkFile = (): string | null => {
    if (!file) return 'Choose a file';
    const ext = file.name.includes('.') ? file.name.slice(file.name.lastIndexOf('.') + 1).toLowerCase() : '';
    if (!settings.allowedExtensions.some((e) => e.replace(/^\./, '').toLowerCase() === ext)) return "This file type isn't allowed";
    if (file.size === 0) return 'The file is empty';
    if (file.size > settings.maxFileSizeBytes) {
      return `The file is larger than the ${formatBytes(settings.maxFileSizeBytes, config.culture)} limit`;
    }
    return null;
  };

  const handleSubmit = form.onSubmit(async (values) => {
    setTopError(null);
    const problem = checkFile();
    setFileError(problem);
    if (problem || !file) return;
    setSubmitting(true);
    try {
      await upload.mutateAsync({ file, metadata: toMetadataInput(values) });
      notifications.show({ color: 'green', message: 'Document uploaded' });
      onClose();
    } catch (error) {
      if (error instanceof UploadFailedError) {
        setTopError(error.message);
      } else {
        let remaining: ApiError | unknown = error;
        if (error instanceof ApiError && error.status === 400 && error.problem?.errors) {
          const fieldErrors = serverFieldErrors(error.problem);
          const fileKey = FILE_KEYS.find((k) => k in fieldErrors);
          if (fileKey) {
            setFileError(fieldErrors[fileKey]);
            const rest = Object.fromEntries(Object.entries(fieldErrors).filter(([k]) => !FILE_KEYS.includes(k)));
            form.setErrors(rest);
            if (Object.keys(rest).length === 0) {
              remaining = undefined;
            } else {
              remaining = new ApiError(error.status, { ...error.problem, errors: Object.fromEntries(Object.entries(rest).map(([k, m]) => [k, [m]])) });
            }
          }
        }
        if (remaining !== undefined) {
          handleMutationError(remaining, {
            entity: 'document',
            form,
            setTopError,
            onClose,
            failureTitle: 'Could not upload the document',
            fieldNames: Object.keys(form.values),
          });
        }
      }
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <Modal opened onClose={onClose} title="Upload document" size="lg">
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
          <DocumentMetadataFields form={form} categories={settings.categories} />
          <Group>
            <Button type="submit" loading={submitting}>
              {submitting ? 'Uploading…' : 'Upload'}
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

export function UploadDocumentModal({ opened, ...rest }: Props) {
  if (!opened) return null;
  return <UploadDocumentModalInner {...rest} />;
}
