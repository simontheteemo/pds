import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type CashflowPath = paths['/api/cashflow/projects/{projectId}'];

export type ProjectCashflow = DeepRequired<CashflowPath['get']['responses'][200]['content']['application/json']>;
export type CashflowMonth = ProjectCashflow['months'][number];
export type CashflowTotals = ProjectCashflow['totals'];
export type UnscheduledSale = ProjectCashflow['unscheduledSales'][number];

/** True when there is nothing to forecast: no outflow, no inflow, nothing unscheduled. */
export function isCashflowEmpty(c: ProjectCashflow): boolean {
  return (
    c.totals.outflow === 0 &&
    c.totals.inflow === 0 &&
    c.totals.unscheduledCost === 0 &&
    c.unscheduledSales.length === 0
  );
}

/** "2026-10" -> "Oct 2026", independent of the browser's time zone. */
export function monthLabel(month: string, culture: string): string {
  const [year, m] = month.split('-').map(Number);
  return new Intl.DateTimeFormat(culture, { month: 'short', year: 'numeric', timeZone: 'UTC' }).format(
    new Date(Date.UTC(year, m - 1, 1)),
  );
}
