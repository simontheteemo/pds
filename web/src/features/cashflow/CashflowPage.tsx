import { Alert, Anchor, Badge, Group, Loader, Stack, Text, Title } from '@mantine/core';
import { Link, useParams } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { formatDate } from '../../shared/format';
import { NotFoundPage } from '../../shared/layout/NotFoundPage';
import { useProjectCashflow } from './api';
import { CashflowChart } from './CashflowChart';
import { CashflowFiguresStrip } from './CashflowFiguresStrip';
import { CashflowTable } from './CashflowTable';

export function CashflowPage() {
  const { id = '' } = useParams();
  const { culture } = useConfig();
  const query = useProjectCashflow(id);

  if (query.error instanceof ApiError && query.error.status === 404) {
    return <NotFoundPage message="This project doesn't exist or was removed." />;
  }
  if (query.error) {
    return (
      <Alert color="red" title="Could not load the cashflow">
        {query.error.message}
      </Alert>
    );
  }
  if (!query.data) return <Loader />;

  const c = query.data;
  const empty =
    c.totals.outflow === 0 &&
    c.totals.inflow === 0 &&
    c.totals.unscheduledCost === 0 &&
    c.unscheduledSales.length === 0;

  return (
    <Stack>
      <Stack gap={4}>
        <Anchor component={Link} to={`/projects/${c.projectId}`} size="sm">
          {c.projectCode} · {c.projectName}
        </Anchor>
        <Group gap="xs">
          <Title order={2}>Cashflow</Title>
          {c.projectArchived && <Badge color="gray">Archived</Badge>}
        </Group>
        <Text size="sm" c="dimmed">
          As of {formatDate(c.asOf, culture)}
        </Text>
      </Stack>

      <Stack gap={4}>
        <Alert color="blue" title="GST basis">
          Costs exclude GST; sale prices are assumed GST-inclusive as recorded in Sales. No GST adjustment is made.
        </Alert>
        <Text size="sm" c="dimmed">
          Remaining cost is spread evenly to the project's completion date.
        </Text>
      </Stack>

      {c.totals.unscheduledCost > 0 && (
        <Alert color="yellow">
          Set the project's planned completion date to phase the remaining cost.{' '}
          <Anchor component={Link} to={`/projects/${c.projectId}/edit`}>
            Edit project
          </Anchor>
        </Alert>
      )}
      {c.unscheduledSales.length > 0 && (
        <Alert color="yellow">
          {c.unscheduledSales.length} contracted sale(s) have no settlement date:{' '}
          {c.unscheduledSales.map((s) => s.unitIdentifier).join(', ')}
        </Alert>
      )}

      {empty ? (
        <Stack gap={4}>
          <Text c="dimmed">Nothing to forecast yet: add budget lines in the cost report or contracted sales.</Text>
          <Group gap="md">
            <Anchor component={Link} to={`/projects/${c.projectId}/costs`}>
              Cost report
            </Anchor>
            <Anchor component={Link} to={`/projects/${c.projectId}`}>
              Project
            </Anchor>
          </Group>
        </Stack>
      ) : (
        <>
          <CashflowFiguresStrip totals={c.totals} currency={c.currency} />
          <CashflowChart months={c.months} currency={c.currency} />
          <CashflowTable months={c.months} totals={c.totals} asOf={c.asOf} currency={c.currency} />
        </>
      )}
    </Stack>
  );
}
