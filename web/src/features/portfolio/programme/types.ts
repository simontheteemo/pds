import type { components, paths } from '../../../shared/api/schema';
import type { DeepRequired } from '../../../shared/api/types';

type MilestonesPath = paths['/api/portfolio/projects/{id}/milestones'];

export type MilestoneList = DeepRequired<MilestonesPath['get']['responses'][200]['content']['application/json']>;
export type MilestoneDetails = MilestoneList['items'][number];
export type MilestoneInput = MilestonesPath['post']['requestBody']['content']['application/json'];
export type MilestoneUpdateInput =
  paths['/api/portfolio/projects/{id}/milestones/{milestoneId}']['put']['requestBody']['content']['application/json'];

export const milestoneCategories = [
  'Land',
  'Design',
  'ResourceConsent',
  'BuildingConsent',
  'Construction',
  'Completion',
  'Titles',
  'Sales',
  'Finance',
  'Other',
] as const satisfies readonly components['schemas']['MilestoneCategory'][];
export type MilestoneCategory = (typeof milestoneCategories)[number];
export const milestoneCategoryLabel: Record<MilestoneCategory, string> = {
  Land: 'Land',
  Design: 'Design',
  ResourceConsent: 'Resource consent',
  BuildingConsent: 'Building consent',
  Construction: 'Construction',
  Completion: 'Completion',
  Titles: 'Titles',
  Sales: 'Sales',
  Finance: 'Finance',
  Other: 'Other',
};

export type MilestoneStatus = components['schemas']['MilestoneStatus'];
export const milestoneStatusLabel: Record<MilestoneStatus, string> = {
  NotStarted: 'Not started',
  OnTrack: 'On track',
  AtRisk: 'At risk',
  Overdue: 'Overdue',
  Complete: 'Complete',
};
export const milestoneStatusColor: Record<MilestoneStatus, string> = {
  NotStarted: 'gray',
  OnTrack: 'green',
  AtRisk: 'yellow',
  Overdue: 'red',
  Complete: 'teal',
};

export const isSlipping = (s: MilestoneStatus) => s === 'AtRisk' || s === 'Overdue';

/** The date the milestone is currently expected: forecast if set, else planned. */
export const dueDate = (m: MilestoneDetails) => m.forecastDate ?? m.plannedDate;
