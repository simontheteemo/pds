import { Alert, Button, Card, Group, Loader, Stack, Text, Title } from '@mantine/core';
import { Link } from 'react-router';
import { useCostReport } from './api';
import { CostFiguresStrip } from './CostFiguresStrip';

export function ProjectCostsCard({ projectId }: { projectId: string }) {
  const report = useCostReport(projectId);
  const link = (
    <Button component={Link} to={`/projects/${projectId}/costs`} variant="light">
      Open cost report
    </Button>
  );

  return (
    <Card withBorder>
      <Title order={4} mb="sm">
        Costs
      </Title>
      {report.error ? (
        <Alert color="red" title="Could not load costs">
          {report.error.message}
        </Alert>
      ) : !report.data ? (
        <Loader size="sm" />
      ) : report.data.lines.length === 0 ? (
        <Group justify="space-between">
          <Text c="dimmed">No budget lines yet.</Text>
          {link}
        </Group>
      ) : (
        <Stack>
          <CostFiguresStrip figures={report.data.totals} currency={report.data.currency} />
          <div>{link}</div>
        </Stack>
      )}
    </Card>
  );
}
