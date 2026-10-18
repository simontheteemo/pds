import { Alert, Badge, Card, Group, Loader, Stack, Text, Title, UnstyledButton } from '@mantine/core';
import { Link } from 'react-router';
import { useProjectAttention } from './attention';
import type { OverviewPanelProps } from './overviewPanels';
import { useProjectTabLink } from './ProjectWorkspaceTabs';

const MAX_ROWS = 8;

export function NeedsAttentionPanel({ project }: OverviewPanelProps) {
  const { items, isLoading, errors } = useProjectAttention(project.id);
  const tabLink = useProjectTabLink();
  const shown = items.slice(0, MAX_ROWS);

  return (
    <Card withBorder h="100%">
      <Group gap="xs" mb="sm">
        <Title order={4}>Needs attention</Title>
        {items.length > 0 && (
          <Badge color="red" variant="light">
            {items.length}
          </Badge>
        )}
      </Group>
      {errors.length > 0 && (
        <Alert color="yellow" p="xs" mb="xs">
          Could not load {errors.join(', ')}; the list may be incomplete.
        </Alert>
      )}
      {isLoading && items.length === 0 ? (
        <Loader size="sm" />
      ) : items.length === 0 ? (
        errors.length === 0 && (
          <Text c="green">&#10003; Nothing needs attention.</Text>
        )
      ) : (
        <Stack gap="xs">
          {shown.map((item) => (
            <UnstyledButton key={item.key} component={Link} to={tabLink(item.tab)} style={{ display: 'block' }}>
              <Group gap="xs" wrap="nowrap" align="flex-start">
                <Badge color={item.severity} variant="light" style={{ flexShrink: 0 }}>
                  {item.badge}
                </Badge>
                <div style={{ minWidth: 0 }}>
                  <Text size="sm" lineClamp={2}>
                    {item.title}
                  </Text>
                  {item.detail && (
                    <Text size="xs" c="dimmed" lineClamp={2}>
                      {item.detail}
                    </Text>
                  )}
                </div>
              </Group>
            </UnstyledButton>
          ))}
          {items.length > MAX_ROWS && (
            <Text size="sm" c="dimmed">
              and {items.length - MAX_ROWS} more
            </Text>
          )}
        </Stack>
      )}
    </Card>
  );
}
