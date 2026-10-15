import { Alert, Code, Loader, Modal, Stack, Text } from '@mantine/core';
import { useEffect } from 'react';
import { usePreviewReminders } from './api';

interface Props {
  opened: boolean;
  onClose: () => void;
}

export function ReminderPreviewModal({ opened, onClose }: Props) {
  const preview = usePreviewReminders();
  const { mutate } = preview;

  useEffect(() => {
    if (opened) mutate();
  }, [opened, mutate]);

  const data = preview.data;

  return (
    <Modal opened={opened} onClose={onClose} title="Reminder email preview" size="xl">
      <Stack>
        {preview.error ? (
          <Alert color="red" title="Could not load the preview">
            {preview.error.message}
          </Alert>
        ) : !data ? (
          <Loader />
        ) : (
          <>
            {data.subject ? (
              <>
                <Text fw={600}>{data.subject}</Text>
                <Code block>{data.body}</Code>
              </>
            ) : (
              <Text>Nothing is due, so no email would be sent.</Text>
            )}
            <Text size="sm" c="dimmed">
              {data.deliveryConfigured
                ? "Reminder emails are sent on this deployment's schedule."
                : 'Reminder emails are not configured for this deployment.'}
            </Text>
          </>
        )}
      </Stack>
    </Modal>
  );
}
