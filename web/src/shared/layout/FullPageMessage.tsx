import { Center, Loader, Stack, Text, Title } from '@mantine/core';

export function FullPageMessage({ title, message, loading = false }: { title: string; message?: string; loading?: boolean }) {
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
      </Stack>
    </Center>
  );
}
