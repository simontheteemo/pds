import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type ProjectsPath = paths['/api/portfolio/projects'];
type ProjectPath = paths['/api/portfolio/projects/{id}'];

export type ProjectDetails = DeepRequired<ProjectPath['get']['responses'][200]['content']['application/json']>;
export type ProjectPage = DeepRequired<ProjectsPath['get']['responses'][200]['content']['application/json']>;
export type ProjectListItem = ProjectPage['items'][number];
export type ProjectInput = ProjectsPath['post']['requestBody']['content']['application/json'];
export type ProjectUpdateInput = ProjectPath['put']['requestBody']['content']['application/json'];

/**
 * Deviation from brief: the generated query parameters for ListProjects are PascalCase
 * (Stage/Status/Search/IncludeArchived/Page/PageSize), matching the ASP.NET Core model
 * binder's parameter names rather than the JSON body casing used everywhere else. This
 * type is the camelCase shape the page and hooks use; `api.ts` maps it onto the wire
 * shape so no cast is needed in ProjectsPage.tsx.
 */
export type ListQuery = {
  stage?: Stage;
  status?: Status;
  search?: string;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
};

export const stages = ['Acquisition', 'Feasibility', 'Design', 'Consenting', 'Construction', 'Sales', 'Completed'] as const;
export type Stage = (typeof stages)[number];

export const statuses = ['OnTrack', 'AtRisk', 'Delayed', 'OnHold', 'Cancelled'] as const;
export type Status = (typeof statuses)[number];

export const stageLabel: Record<Stage, string> = {
  Acquisition: 'Acquisition',
  Feasibility: 'Feasibility',
  Design: 'Design',
  Consenting: 'Consenting',
  Construction: 'Construction',
  Sales: 'Sales',
  Completed: 'Completed',
};

export const statusLabel: Record<Status, string> = {
  OnTrack: 'On track',
  AtRisk: 'At risk',
  Delayed: 'Delayed',
  OnHold: 'On hold',
  Cancelled: 'Cancelled',
};

export const statusColor: Record<Status, string> = {
  OnTrack: 'green',
  AtRisk: 'yellow',
  Delayed: 'red',
  OnHold: 'gray',
  Cancelled: 'dark',
};
