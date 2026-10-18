/**
 * Registry of panels shown on the project Overview tab, in display order.
 *
 * To add a panel:
 *  1. Write a component taking `OverviewPanelProps` ({ project }). It should load its own data.
 *  2. Add one entry to `overviewPanels` below: `{ id: 'my-panel', span: 'half', Panel: MyPanel }`.
 *     `span: 'full'` takes the whole row; `'half'` shares a row on md+ screens.
 * Link to other tabs with `useProjectTabLink()` from `./ProjectWorkspaceTabs`.
 */
import type { ComponentType } from 'react';
import type { ProjectDetails } from '../types';
import { CostsPanel } from './CostsPanel';
import { KeyFactsPanel } from './KeyFactsPanel';
import { NeedsAttentionPanel } from './NeedsAttentionPanel';
import { NextMilestonesPanel } from './NextMilestonesPanel';
import { SalesPositionPanel } from './SalesPositionPanel';

export type OverviewPanelProps = { project: ProjectDetails };
export type OverviewPanel = { id: string; span: 'full' | 'half'; Panel: ComponentType<OverviewPanelProps> };

export const overviewPanels: OverviewPanel[] = [
  { id: 'key-facts', span: 'full', Panel: KeyFactsPanel },
  { id: 'needs-attention', span: 'half', Panel: NeedsAttentionPanel },
  { id: 'next-milestones', span: 'half', Panel: NextMilestonesPanel },
  { id: 'costs', span: 'half', Panel: CostsPanel },
  { id: 'sales', span: 'half', Panel: SalesPositionPanel },
];
