import { Alert, Button, Card, Group, Loader, Stack, Text, Title } from '@mantine/core';
import { Link } from 'react-router';
import { useConfig } from '../../shared/config';
import { formatMoney } from '../../shared/format';
import { useProjectCashflow } from './api';
import { monthLabel } from './types';

export function ProjectCashflowCard({ projectId }: { projectId: string }) {
  const { culture } = useConfig();
  const query = useProjectCashflow(projectId);
  const link = (
    <Button component={Link} to={`/projects/${projectId}/cashflow`} variant="light">
      Open cashflow
    </Button>
  );

  const c = query.data;
  const empty =
    c &&
    c.totals.outflow === 0 &&
    c.totals.inflow === 0 &&
    c.totals.unscheduledCost === 0 &&
    c.unscheduledSales.length === 0;

  return (
    <Card withBorder>
      <Title order={4} mb="sm">
        Cashflow
      </Title>
      {query.error ? (
        <Alert color="red" title="Could not load cashflow">
          {query.error.message}
        </Alert>
      ) : !c ? (
        <Loader size="sm" />
      ) : empty ? (
        <Group justify="space-between">
          <Text c="dimmed">Nothing to forecast yet.</Text>
          {link}
        </Group>
      ) : (
        <Stack>
          <Group gap="xl">
            <Text>
              {c.totals.peakFunding === null || c.totals.peakFundingMonth === null
                ? 'No funding gap forecast'
                : `Peak funding ${formatMoney(c.totals.peakFunding, c.currency, culture)} in ${monthLabel(c.totals.peakFundingMonth, culture)}`}
            </Text>
            <Text>Net position {formatMoney(c.totals.net, c.currency, culture)}</Text>
          </Group>
          <div>{link}</div>
        </Stack>
      )}
    </Card>
  );
}
