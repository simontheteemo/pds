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
import { KeyFactsPanel } from './KeyFactsPanel';

export type OverviewPanelProps = { project: ProjectDetails };
export type OverviewPanel = { id: string; span: 'full' | 'half'; Panel: ComponentType<OverviewPanelProps> };

export const overviewPanels: OverviewPanel[] = [{ id: 'key-facts', span: 'full', Panel: KeyFactsPanel }];
