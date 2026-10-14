import type { components, paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type UnitsPath = paths['/api/sales/projects/{projectId}/units'];
type UnitPath = paths['/api/sales/projects/{projectId}/units/{unitId}'];
type SalePath = paths['/api/sales/projects/{projectId}/units/{unitId}/sale'];
type SummaryPath = paths['/api/sales/summary'];

export type ProjectSales = DeepRequired<UnitsPath['get']['responses'][200]['content']['application/json']>;
export type UnitDetails = ProjectSales['units'][number];
export type SalesSummary = ProjectSales['summary'];
export type ProjectSalesSummary = DeepRequired<SummaryPath['get']['responses'][200]['content']['application/json']>[number];
export type UnitInput = UnitsPath['post']['requestBody']['content']['application/json'];
export type UnitUpdateInput = UnitPath['put']['requestBody']['content']['application/json'];
export type SaleInput = SalePath['put']['requestBody']['content']['application/json'];

export const unitTypes = ['Apartment', 'Townhouse', 'House', 'Lot', 'Commercial', 'Other'] as const;
export type UnitType = (typeof unitTypes)[number];

export const saleStatuses = ['Available', 'Reserved', 'Conditional', 'Unconditional', 'Settled', 'Withdrawn'] as const;
export type SaleStatus = (typeof saleStatuses)[number];

// Compile-time checks that the local unions match the generated enums exactly (both directions).
type Equals<A, B> = [A] extends [B] ? ([B] extends [A] ? true : false) : false;
const unitTypesMatchSchema: Equals<UnitType, components['schemas']['UnitType']> = true;
const saleStatusesMatchSchema: Equals<SaleStatus, components['schemas']['SaleStatus']> = true;
void unitTypesMatchSchema;
void saleStatusesMatchSchema;

export const unitTypeLabel: Record<UnitType, string> = {
  Apartment: 'Apartment',
  Townhouse: 'Townhouse',
  House: 'House',
  Lot: 'Lot',
  Commercial: 'Commercial',
  Other: 'Other',
};

export const saleStatusLabel: Record<SaleStatus, string> = {
  Available: 'Available',
  Reserved: 'Reserved / under offer',
  Conditional: 'Conditional',
  Unconditional: 'Unconditional',
  Settled: 'Settled',
  Withdrawn: 'Withdrawn',
};

export const saleStatusColor: Record<SaleStatus, string> = {
  Available: 'blue',
  Reserved: 'yellow',
  Conditional: 'orange',
  Unconditional: 'teal',
  Settled: 'green',
  Withdrawn: 'gray',
};

/** Mirrors the server's SaleRules: Available and Withdrawn carry no sale; every other status does (and needs a buyer). */
export function hasSale(status: SaleStatus): boolean {
  return status !== 'Available' && status !== 'Withdrawn';
}

/** S&P agreement date and sale price are required (Conditional or later). */
export function requiresAgreement(status: SaleStatus): boolean {
  return status === 'Conditional' || status === 'Unconditional' || status === 'Settled';
}

export function requiresUnconditional(status: SaleStatus): boolean {
  return status === 'Unconditional' || status === 'Settled';
}

export function requiresSettlement(status: SaleStatus): boolean {
  return status === 'Settled';
}

/** (Unconditional + Settled) / (total − Withdrawn), or null when no units count. */
export function soldPercent(summary: SalesSummary): number | null {
  const denominator = summary.totalUnits - summary.withdrawn;
  return denominator > 0 ? (summary.unconditional + summary.settled) / denominator : null;
}
