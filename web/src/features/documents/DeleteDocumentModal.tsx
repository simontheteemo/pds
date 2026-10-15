import { Button, Group, Modal, Stack, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { ApiError } from '../../shared/api/client';
import { useDeleteDocument } from './api';
import type { DocumentDetails } from './types';

type Props = { projectId: string; document: DocumentDetails | undefined; onClose: () => void };

export function DeleteDocumentModal({ projectId, document, onClose }: Props) {
  const remove = useDeleteDocument(projectId);

  const confirm = async () => {
    if (!document) return;
    try {
      await remove.mutateAsync({ documentId: document.id, version: document.version });
      notifications.show({ color: 'green', message: 'Document deleted' });
      onClose();
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        notifications.show({
          color: 'yellow',
          title: 'This document was changed by someone else',
          message: 'The list now shows the latest version. Open it again to make your changes.',
        });
        onClose();
      } else {
        notifications.show({
          color: 'red',
          title: 'Could not delete the document',
          message: error instanceof Error ? error.message : 'Unexpected error',
        });
      }
    }
  };

  const busy = remove.isPending;
  return (
    <Modal
      opened={document !== undefined}
      onClose={onClose}
      title="Delete document"
      closeOnClickOutside={!busy}
      closeOnEscape={!busy}
      withCloseButton={!busy}
    >
      <Stack gap="md">
        <Text>
          Delete “{document?.title}”? It will no longer appear in the register. Files are kept and an
          administrator can restore them on request.
        </Text>
        <Group>
          <Button color="red" loading={busy} onClick={confirm}>
            Delete
          </Button>
          <Button variant="default" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
