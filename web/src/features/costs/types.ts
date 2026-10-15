import type { paths } from '../../shared/api/schema';
import type { components } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type ReportPath = paths['/api/costs/projects/{projectId}'];
type LinesPath = paths['/api/costs/projects/{projectId}/lines'];

export type ProjectCostReport = DeepRequired<ReportPath['get']['responses'][200]['content']['application/json']>;
export type CostFigures = ProjectCostReport['totals'];
export type CostReportLine = ProjectCostReport['lines'][number];
export type CostReportCommitment = ProjectCostReport['commitments'][number];
export type CostReportActual = ProjectCostReport['actuals'][number];
export type CostReportContract = ProjectCostReport['contracts'][number];
export type BudgetLineInput = LinesPath['post']['requestBody']['content']['application/json'];
export type BudgetLineUpdateInput =
  paths['/api/costs/projects/{projectId}/lines/{lineId}']['put']['requestBody']['content']['application/json'];

export type CommitmentInput =
  paths['/api/costs/projects/{projectId}/commitments']['post']['requestBody']['content']['application/json'];
export type CommitmentUpdateInput =
  paths['/api/costs/projects/{projectId}/commitments/{entryId}']['put']['requestBody']['content']['application/json'];
export type ActualCostInput =
  paths['/api/costs/projects/{projectId}/actuals']['post']['requestBody']['content']['application/json'];
export type ActualCostUpdateInput =
  paths['/api/costs/projects/{projectId}/actuals/{entryId}']['put']['requestBody']['content']['application/json'];

export const commitmentTypes = ['Contract', 'Variation', 'Order'] as const satisfies readonly components['schemas']['CommitmentType'][];
export type CommitmentType = (typeof commitmentTypes)[number];
export const commitmentTypeLabel: Record<CommitmentType, string> = {
  Contract: 'Contract',
  Variation: 'Variation',
  Order: 'Order',
};

export const actualCostTypes = ['Invoice', 'Claim', 'Other'] as const satisfies readonly components['schemas']['ActualCostType'][];
export type ActualCostType = (typeof actualCostTypes)[number];
export const actualCostTypeLabel: Record<ActualCostType, string> = {
  Invoice: 'Invoice',
  Claim: 'Payment claim',
  Other: 'Other',
};
