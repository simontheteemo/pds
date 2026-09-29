import { Button, Stack, Text, Title } from '@mantine/core';
import { Link } from 'react-router';

export function NotFoundPage({ message = "This page doesn't exist." }: { message?: string }) {
  return (
    <Stack align="flex-start">
      <Title order={2}>Not found</Title>
      <Text>{message}</Text>
      <Button component={Link} to="/projects" variant="light">
        Back to projects
      </Button>
    </Stack>
  );
}
