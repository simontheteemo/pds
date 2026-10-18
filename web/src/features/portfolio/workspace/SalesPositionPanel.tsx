import { Alert, Anchor, Card, Loader, Text, Title } from '@mantine/core';
import { Link } from 'react-router';
import { useProjectSales } from '../../sales/api';
import { SalesSummaryStrip } from '../../sales/SalesSummaryStrip';
import type { OverviewPanelProps } from './overviewPanels';
import { useProjectTabLink } from './ProjectWorkspaceTabs';

export function SalesPositionPanel({ project }: OverviewPanelProps) {
  const sales = useProjectSales(project.id);
  const tabLink = useProjectTabLink();

  return (
    <Card withBorder h="100%">
      <Title order={4} mb="sm">
        Sales position
      </Title>
      {sales.isPending ? (
        <Loader size="sm" />
      ) : sales.isError ? (
        <Alert color="red">Could not load sales.</Alert>
      ) : sales.data.units.length > 0 ? (
        <SalesSummaryStrip summary={sales.data.summary} />
      ) : (
        <>
          <Text c="dimmed">No units recorded.</Text>
          <Anchor component={Link} to={tabLink('sales')} size="sm" mt="xs" display="block">
            Open sales
          </Anchor>
        </>
      )}
    </Card>
  );
}
