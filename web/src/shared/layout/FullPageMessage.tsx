import { Button, Center, Loader, Stack, Text, Title } from '@mantine/core';
import type { ReactNode } from 'react';

export function FullPageMessage({
  title,
  message,
  loading = false,
  action,
}: {
  title: string;
  message?: string;
  loading?: boolean;
  action?: { label: ReactNode; onClick: () => void };
}) {
  return (
    <Center mih="100vh" p="md">
      <Stack align="center" gap="xs" maw={480}>
        {loading && <Loader />}
        <Title order={3}>{title}</Title>
        {message && (
          <Text c="dimmed" ta="center">
            {message}
          </Text>
        )}
        {action && (
          <Button mt="sm" onClick={action.onClick}>
            {action.label}
          </Button>
        )}
      </Stack>
    </Center>
  );
}
