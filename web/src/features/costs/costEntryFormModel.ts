import { z } from 'zod';
import {
  actualCostTypes,
  commitmentTypes,
  type ActualCostInput,
  type CommitmentInput,
  type CostReportActual,
  type CostReportCommitment,
} from './types';

export type CostEntryKind = 'commitment' | 'actual';

export type CostEntryFormValues = {
  budgetLineId: string | null;
  type: string | null;
  contractId: string | null;
  payee: string;
  description: string;
  reference: string;
  date: string;
  amount: number | '';
  notes: string;
};

export function todayIso(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function emptyCostEntryValues(budgetLineId: string | null): CostEntryFormValues {
  return {
    budgetLineId,
    type: null,
    contractId: null,
    payee: '',
    description: '',
    reference: '',
    date: todayIso(),
    amount: '',
    notes: '',
  };
}

const maxDp = (dp: number) => (v: number | '') =>
  v === '' || Math.abs(Math.round(v * 10 ** dp) - v * 10 ** dp) < 1e-6;

/** Mirrors the API's commitment / actual validators. */
export function costEntrySchema(kind: CostEntryKind) {
  const types: readonly string[] = kind === 'commitment' ? commitmentTypes : actualCostTypes;
  return z
    .object({
      budgetLineId: z.string().nullable().refine((v) => v !== null, 'Budget line is required'),
      type: z.string().nullable().refine((v) => v !== null && types.includes(v), 'Type is required'),
      contractId: z.string().nullable(),
      payee: z.string().max(200, 'Use 200 characters or fewer'),
      description: z.string().trim().min(1, 'Description is required').max(500, 'Use 500 characters or fewer'),
      reference: z.string().max(100, 'Use 100 characters or fewer'),
      date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Date is required'),
      amount: z
        .union([z.number(), z.literal('')])
        .refine((v) => v !== '', 'Amount is required')
        .refine(maxDp(2), 'Use at most 2 decimal places'),
      notes: z.string().max(2000, 'Use 2000 characters or fewer'),
    })
    .superRefine((v, ctx) => {
      const amount: unknown = v.amount;
      if (typeof amount !== 'number') return;
      if (kind === 'commitment' && amount < 0 && v.type !== 'Variation') {
        ctx.addIssue({ code: 'custom', path: ['amount'], message: 'Only a variation can be negative.' });
      }
      if (kind === 'actual' && amount === 0) {
        ctx.addIssue({ code: 'custom', path: ['amount'], message: 'Amount cannot be zero' });
      }
    });
}

const orNull = (s: string) => (s.trim() === '' ? null : s.trim());

export function toCommitmentInput(v: CostEntryFormValues): CommitmentInput {
  return {
    budgetLineId: v.budgetLineId,
    type: commitmentTypes.find((t) => t === v.type) ?? null,
    contractId: v.contractId,
    description: v.description.trim(),
    reference: orNull(v.reference),
    date: v.date,
    amount: v.amount === '' ? null : v.amount,
    notes: orNull(v.notes),
  };
}

export function toActualCostInput(v: CostEntryFormValues): ActualCostInput {
  return { ...toCommitmentInput(v), type: actualCostTypes.find((t) => t === v.type) ?? null, payee: orNull(v.payee) };
}

export function fromEntry(e: CostReportCommitment['entry'] | CostReportActual['entry']): CostEntryFormValues {
  return {
    budgetLineId: e.budgetLineId,
    type: e.type,
    contractId: e.contractId,
    payee: 'payee' in e ? (e.payee ?? '') : '',
    description: e.description,
    reference: e.reference ?? '',
    date: e.date,
    amount: e.amount,
    notes: e.notes ?? '',
  };
}
