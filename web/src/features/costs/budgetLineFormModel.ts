import { z } from 'zod';
import type { BudgetLineInput, CostReportLine } from './types';

export type BudgetLineFormValues = {
  name: string;
  sortOrder: number | '';
  budgetAmount: number | '';
  forecastRemainingAmount: number | '';
  notes: string;
};

export function emptyBudgetLineValues(existing: CostReportLine[]): BudgetLineFormValues {
  const max = existing.reduce((m, l) => Math.max(m, l.line.sortOrder), 0);
  return { name: '', sortOrder: Math.min(max + 10, 9999), budgetAmount: '', forecastRemainingAmount: '', notes: '' };
}

const maxDp = (dp: number) => (v: number | '') =>
  v === '' || Math.abs(Math.round(v * 10 ** dp) - v * 10 ** dp) < 1e-6;

/** Mirrors the API's budget line validators. */
export const budgetLineSchema = z.object({
  name: z.string().trim().min(1, 'Name is required').max(100, 'Use 100 characters or fewer'),
  sortOrder: z
    .union([z.number(), z.literal('')])
    .refine((v) => v !== '' && Number.isInteger(v) && v >= 0 && v <= 9999, 'Enter a whole number from 0 to 9999'),
  budgetAmount: z
    .union([z.number().min(0, 'Budget must be 0 or more'), z.literal('')])
    .refine((v) => v !== '', 'Budget is required')
    .refine(maxDp(2), 'Use at most 2 decimal places'),
  forecastRemainingAmount: z
    .union([z.number().min(0, 'Forecast remaining must be 0 or more'), z.literal('')])
    .refine(maxDp(2), 'Use at most 2 decimal places'),
  notes: z.string().max(2000, 'Use 2000 characters or fewer'),
});

export function toBudgetLineInput(v: BudgetLineFormValues): BudgetLineInput {
  return {
    name: v.name.trim(),
    sortOrder: v.sortOrder === '' ? null : v.sortOrder,
    budgetAmount: v.budgetAmount === '' ? null : v.budgetAmount,
    forecastRemainingAmount: v.forecastRemainingAmount === '' ? null : v.forecastRemainingAmount,
    notes: v.notes.trim() === '' ? null : v.notes.trim(),
  };
}

export function fromBudgetLine(l: CostReportLine['line']): BudgetLineFormValues {
  return {
    name: l.name,
    sortOrder: l.sortOrder,
    budgetAmount: l.budgetAmount,
    forecastRemainingAmount: l.forecastRemainingAmount ?? '',
    notes: l.notes ?? '',
  };
}
