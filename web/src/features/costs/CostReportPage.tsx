import { Alert, Anchor, Badge, Group, Loader, Stack, Tabs, Text, Title } from '@mantine/core';
import { Link, useParams } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { NotFoundPage } from '../../shared/layout/NotFoundPage';
import { useCostReport } from './api';
import { ActualsTab } from './ActualsTab';
import { BudgetTab } from './BudgetTab';
import { CommitmentsTab } from './CommitmentsTab';
import { CostFiguresStrip } from './CostFiguresStrip';

export function CostReportPage() {
  const { id = '' } = useParams();
  const report = useCostReport(id);

  if (report.error instanceof ApiError && report.error.status === 404) {
    return <NotFoundPage message="This project doesn't exist or was removed." />;
  }
  if (report.error) {
    return (
      <Alert color="red" title="Could not load the cost report">
        {report.error.message}
      </Alert>
    );
  }
  if (!report.data) return <Loader />;

  const r = report.data;
  return (
    <Stack>
      <Stack gap={4}>
        <Anchor component={Link} to={`/projects/${r.projectId}`} size="sm">
          {r.projectCode} · {r.projectName}
        </Anchor>
        <Group gap="xs">
          <Title order={2}>Cost report</Title>
          {r.projectArchived && <Badge color="gray">Archived</Badge>}
        </Group>
        <Text size="sm" c="dimmed">
          Amounts exclude GST
        </Text>
      </Stack>

      <CostFiguresStrip figures={r.totals} currency={r.currency} showFtc />

      <Tabs defaultValue="budget" keepMounted={false}>
        <Tabs.List>
          <Tabs.Tab value="budget">Budget</Tabs.Tab>
          <Tabs.Tab value="commitments">Commitments</Tabs.Tab>
          <Tabs.Tab value="actuals">Invoices &amp; claims</Tabs.Tab>
        </Tabs.List>
        <Tabs.Panel value="budget" pt="md">
          <BudgetTab report={r} />
        </Tabs.Panel>
        <Tabs.Panel value="commitments" pt="md">
          <CommitmentsTab report={r} />
        </Tabs.Panel>
        <Tabs.Panel value="actuals" pt="md">
          <ActualsTab report={r} />
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}
