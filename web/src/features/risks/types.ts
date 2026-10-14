import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type RisksPath = paths['/api/risks/projects/{projectId}/risks'];
type RiskPath = paths['/api/risks/projects/{projectId}/risks/{riskId}'];

export type RiskDetails = DeepRequired<RisksPath['get']['responses'][200]['content']['application/json']>[number];
export type RiskInput = RisksPath['post']['requestBody']['content']['application/json'];
export type RiskUpdateInput = RiskPath['put']['requestBody']['content']['application/json'];

export const categories = [
  'Geotechnical', 'Consenting', 'Contractual', 'ContractorDefault', 'Market',
  'Financial', 'Design', 'HealthAndSafety', 'Environmental', 'Other',
] as const;
export type Category = (typeof categories)[number];

export const categoryLabel: Record<Category, string> = {
  Geotechnical: 'Geotechnical',
  Consenting: 'Consenting',
  Contractual: 'Contractual / dispute',
  ContractorDefault: 'Contractor default',
  Market: 'Market',
  Financial: 'Financial',
  Design: 'Design',
  HealthAndSafety: 'Health & safety',
  Environmental: 'Environmental',
  Other: 'Other',
};

export type Band = 'Low' | 'Medium' | 'High' | 'Extreme';
export const bandColor: Record<Band, string> = { Low: 'green', Medium: 'yellow', High: 'orange', Extreme: 'red' };

export type RiskStatus = 'Open' | 'Mitigating' | 'Closed';
export const statusLabel: Record<RiskStatus, string> = { Open: 'Open', Mitigating: 'Mitigating', Closed: 'Closed' };

export const likelihoodLabels = ['Rare', 'Unlikely', 'Possible', 'Likely', 'Almost certain'] as const;
export const impactLabels = ['Insignificant', 'Minor', 'Moderate', 'Major', 'Severe'] as const;

/** Mirrors the server's RiskRules.BandFor. */
export function bandFor(score: number): Band {
  if (score <= 4) return 'Low';
  if (score <= 9) return 'Medium';
  if (score <= 16) return 'High';
  return 'Extreme';
}
