import { SimpleGrid } from '@mantine/core';
import { overviewPanels } from './overviewPanels';
import type { ProjectTabProps } from './projectTabs';

export function OverviewTab({ project }: ProjectTabProps) {
  return (
    <SimpleGrid cols={{ base: 1, md: 2 }}>
      {overviewPanels.map(({ id, span, Panel }) =>
        span === 'full' ? (
          <div key={id} style={{ gridColumn: '1 / -1' }}>
            <Panel project={project} />
          </div>
        ) : (
          <div key={id}>
            <Panel project={project} />
          </div>
        ),
      )}
    </SimpleGrid>
  );
}
