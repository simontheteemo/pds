import { Alert, Button, FileInput, Group, Modal, Stack, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { useConfig } from '../../shared/config';
import { useAddVersion } from './api';
import { formatBytes, validateFile } from './format';
import type { DocumentDetails, DocumentSettings } from './types';
import { handleUploadError } from './uploadError';

type Props = {
  projectId: string;
  document: DocumentDetails | undefined;
  settings: DocumentSettings;
  onClose: () => void;
};

function NewVersionForm({ projectId, document, settings, onClose }: Props & { document: DocumentDetails }) {
  const config = useConfig();
  const addVersion = useAddVersion(projectId);
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [topError, setTopError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setTopError(null);
    const problem = validateFile(file, settings, config.culture);
    setFileError(problem);
    if (problem || !file) return;
    setSubmitting(true);
    try {
      await addVersion.mutateAsync({ documentId: document.id, file, version: document.version });
      notifications.show({ color: 'green', message: `Version ${document.currentVersion + 1} uploaded` });
      onClose();
    } catch (error) {
      handleUploadError(error, {
        fieldNames: [],
        setFileError,
        setTopError,
        onClose,
        failureTitle: 'Could not upload the new version',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      opened
      onClose={onClose}
      title={`New version of “${document.title}”`}
      size="lg"
      closeOnClickOutside={!submitting}
      closeOnEscape={!submitting}
      withCloseButton={!submitting}
    >
      <form onSubmit={handleSubmit} noValidate>
        <Stack gap="md">
          {topError && <Alert color="red">{topError}</Alert>}
          <Text size="sm" c="dimmed">
            Currently v{document.currentVersion}: {document.fileName}
          </Text>
          <FileInput
            label="File"
            withAsterisk
            description={`Up to ${formatBytes(settings.maxFileSizeBytes, config.culture)}. Allowed: ${settings.allowedExtensions.join(', ')}`}
            accept={settings.allowedExtensions.map((e) => (e.startsWith('.') ? e : `.${e}`)).join(',')}
            value={file}
            onChange={(next) => {
              setFile(next);
              setFileError(null);
            }}
            error={fileError}
            clearable
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

export function NewVersionModal(props: Props) {
  const { document } = props;
  if (!document) return null;
  return <NewVersionForm key={`${document.id}-${document.version}`} {...props} document={document} />;
}
