/**
 * Registry of project workspace tabs, in display order.
 *
 * To add a tab: write a component taking `ProjectTabProps` ({ project }) and add one entry to
 * `projectTabs`: `{ key: 'my-tab', label: 'My tab', Panel: MyTab }`. `key` is the `?tab=` URL value.
 * Optionally set `useAttentionCount: (projectId) => number` to show a red count badge on the tab
 * label (0 hides it). It is called as a hook, so it must follow the rules of hooks.
 * Panels mount only while their tab is active, so each loads its own data on first visit.
 */
import type { ComponentType } from 'react';
import { Stack } from '@mantine/core';
import { ProjectCashflowCard } from '../../cashflow/ProjectCashflowCard';
import { ProjectConsentsSection } from '../../consents/ProjectConsentsSection';
import { ProjectContractsSection } from '../../contractors/ProjectContractsSection';
import { ProjectCostsCard } from '../../costs/ProjectCostsCard';
import { ProjectDocumentsSection } from '../../documents/ProjectDocumentsSection';
import { ProjectRisksSection } from '../../risks/ProjectRisksSection';
import { ProjectSalesSection } from '../../sales/ProjectSalesSection';
import { useSlippingMilestoneCount } from '../programme/api';
import { ProgrammeTab } from '../programme/ProgrammeTab';
import type { ProjectDetails } from '../types';
import { useConsentAttentionCount, useRiskAttentionCount } from './attention';
import { OverviewTab } from './OverviewTab';

export type ProjectTabProps = { project: ProjectDetails };
export type ProjectTab = {
  /** URL value of ?tab= */
  key: string;
  label: string;
  Panel: ComponentType<ProjectTabProps>;
  /** Optional: returns a count shown as a red badge on the tab label (0 hides it). */
  useAttentionCount?: (projectId: string) => number;
};

export const DEFAULT_TAB = 'overview';

export const projectTabs: ProjectTab[] = [
  { key: 'overview', label: 'Overview', Panel: OverviewTab },
  { key: 'programme', label: 'Programme', Panel: ProgrammeTab, useAttentionCount: useSlippingMilestoneCount },
  {
    key: 'costs',
    label: 'Costs',
    Panel: ({ project }) => (
      <Stack>
        <ProjectCostsCard projectId={project.id} />
        <ProjectCashflowCard projectId={project.id} />
      </Stack>
    ),
  },
  {
    key: 'contracts',
    label: 'Contracts',
    Panel: ({ project }) => <ProjectContractsSection projectId={project.id} projectArchived={project.isArchived} />,
  },
  {
    key: 'consents',
    label: 'Consents',
    Panel: ({ project }) => <ProjectConsentsSection projectId={project.id} projectArchived={project.isArchived} />,
    useAttentionCount: useConsentAttentionCount,
  },
  {
    key: 'sales',
    label: 'Sales',
    Panel: ({ project }) => <ProjectSalesSection projectId={project.id} projectArchived={project.isArchived} />,
  },
  {
    key: 'documents',
    label: 'Documents',
    Panel: ({ project }) => <ProjectDocumentsSection projectId={project.id} projectArchived={project.isArchived} />,
  },
  {
    key: 'risks',
    label: 'Risks',
    Panel: ({ project }) => <ProjectRisksSection projectId={project.id} projectArchived={project.isArchived} />,
    useAttentionCount: useRiskAttentionCount,
  },
];
