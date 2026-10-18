import { Badge, Group, Paper, SimpleGrid, Skeleton, Stack, Text } from '@mantine/core';
import { useConfig } from '../../shared/config';
import { formatMoney } from '../../shared/format';
import { usePortfolioSummary } from './api';
import { stageLabel } from './types';

function Figure({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <Stack gap={2}>
      <Text size="xs" c="dimmed" tt="uppercase">
        {label}
      </Text>
      {children}
    </Stack>
  );
}

export function PortfolioSummaryStrip() {
  const config = useConfig();
  const summary = usePortfolioSummary();

  if (summary.isLoading) return <Skeleton height={64} />;
  if (!summary.data) return null;
  const s = summary.data;
  const slipping = s.projectsSlipping > 0 || s.overdueMilestones > 0;

  return (
    <Paper withBorder p="md">
      <Stack gap="sm">
        <SimpleGrid cols={{ base: 2, sm: 4 }}>
          <Figure label="Active projects">
            <Text fw={600} size="xl">
              {s.activeProjects}
            </Text>
          </Figure>
          <Figure label="Total budget">
            <Text fw={600} size="xl" style={{ fontVariantNumeric: 'tabular-nums' }}>
              {formatMoney(s.totalBudget, s.currency, config.culture)}
            </Text>
          </Figure>
          <Figure label="Slipping">
            <Text fw={600} c={slipping ? 'red' : undefined}>
              {s.projectsSlipping} {s.projectsSlipping === 1 ? 'project' : 'projects'} · {s.overdueMilestones}{' '}
              {s.overdueMilestones === 1 ? 'milestone' : 'milestones'} overdue
            </Text>
          </Figure>
        </SimpleGrid>
        <Group gap="xs" wrap="wrap">
          {s.byStage.map((entry) => (
            <Badge key={entry.stage} variant="light">
              {stageLabel[entry.stage]} {entry.count}
            </Badge>
          ))}
        </Group>
      </Stack>
    </Paper>
  );
}
