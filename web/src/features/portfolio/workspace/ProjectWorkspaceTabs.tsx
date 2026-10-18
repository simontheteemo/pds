import { Badge, Tabs } from '@mantine/core';
import { useLocation, useSearchParams } from 'react-router';
import type { ProjectDetails } from '../types';
import { DEFAULT_TAB, projectTabs, type ProjectTab } from './projectTabs';

/** Returns a function building a relative href to a tab of the current project page (`?tab=<key>`). */
export function useProjectTabLink(): (tab: string) => string {
  const { pathname } = useLocation();
  return (tab) => (tab === DEFAULT_TAB ? pathname : `${pathname}?tab=${encodeURIComponent(tab)}`);
}

function CountBadge({ count }: { count: number }) {
  if (count <= 0) return null;
  return (
    <Badge size="xs" circle color="red" ml={6}>
      {count}
    </Badge>
  );
}

function CountedTabLabel({
  label,
  projectId,
  useCount,
}: {
  label: string;
  projectId: string;
  useCount: (projectId: string) => number;
}) {
  const count = useCount(projectId);
  return (
    <>
      {label}
      <CountBadge count={count} />
    </>
  );
}

// Chosen per tab statically (a tab's useAttentionCount never appears or disappears), so hook order is stable.
function TabLabel({ tab, projectId }: { tab: ProjectTab; projectId: string }) {
  return tab.useAttentionCount ? (
    <CountedTabLabel label={tab.label} projectId={projectId} useCount={tab.useAttentionCount} />
  ) : (
    <>{tab.label}</>
  );
}

export function ProjectWorkspaceTabs({ project }: { project: ProjectDetails }) {
  const [searchParams, setSearchParams] = useSearchParams();
  const requested = searchParams.get('tab');
  const active = projectTabs.some((t) => t.key === requested) ? (requested as string) : DEFAULT_TAB;

  const onChange = (value: string | null) => {
    const next = new URLSearchParams(searchParams);
    if (!value || value === DEFAULT_TAB) next.delete('tab');
    else next.set('tab', value);
    setSearchParams(next); // push, so Back returns to the previous tab
  };

  return (
    <Tabs value={active} onChange={onChange} keepMounted={false}>
      <div style={{ overflowX: 'auto', maxWidth: '100%' }}>
        <Tabs.List style={{ flexWrap: 'nowrap' }}>
          {projectTabs.map((tab) => (
            <Tabs.Tab key={tab.key} value={tab.key} style={{ flexShrink: 0 }}>
              <TabLabel tab={tab} projectId={project.id} />
            </Tabs.Tab>
          ))}
        </Tabs.List>
      </div>
      {projectTabs.map((tab) => (
        <Tabs.Panel key={tab.key} value={tab.key} pt="md">
          <tab.Panel project={project} />
        </Tabs.Panel>
      ))}
    </Tabs>
  );
}
