import { z } from 'zod';
import {
  actualCostTypes,
  commitmentTypes,
  invoiceStatuses,
  type ActualCostInput,
  type CommitmentInput,
  type CostReportActual,
  type CostReportCommitment,
  type CostReportContract,
} from './types';

export type CostEntryKind = 'commitment' | 'actual';

/** One form model for both kinds; the invoice-only fields (contractor .. GST) are ignored for commitments. */
export type CostEntryFormValues = {
  budgetLineId: string | null;
  type: string | null;
  contractId: string | null;
  contractorId: string | null;
  payee: string;
  description: string;
  reference: string;
  date: string;
  dueDate: string;
  status: string | null;
  paidDate: string;
  amount: number | '';
  gstAmount: number | '';
  notes: string;
};

export function todayIso(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function emptyCostEntryValues(
  budgetLineId: string | null,
  contractorId: string | null = null,
): CostEntryFormValues {
  return {
    budgetLineId,
    type: null,
    contractId: null,
    contractorId,
    payee: '',
    description: '',
    reference: '',
    date: todayIso(),
    dueDate: '',
    status: 'Received',
    paidDate: '',
    amount: '',
    gstAmount: '',
    notes: '',
  };
}

const maxDp = (dp: number) => (v: number | '') =>
  v === '' || Math.abs(Math.round(v * 10 ** dp) - v * 10 ** dp) < 1e-6;

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const optionalDate = z.string().refine((v) => v === '' || ISO_DATE.test(v), 'Enter a valid date');

/** Mirrors the API's commitment / actual validators. */
export function costEntrySchema(kind: CostEntryKind) {
  const types: readonly string[] = kind === 'commitment' ? commitmentTypes : actualCostTypes;
  const statuses: readonly string[] = invoiceStatuses;
  return z
    .object({
      budgetLineId: z.string().nullable().refine((v) => v !== null, 'Budget line is required'),
      type: z.string().nullable().refine((v) => v !== null && types.includes(v), 'Type is required'),
      contractId: z.string().nullable(),
      contractorId: z.string().nullable(),
      payee: z.string().max(200, 'Use 200 characters or fewer'),
      description: z.string().trim().min(1, 'Description is required').max(200, 'Use 200 characters or fewer'),
      reference: z.string().max(50, 'Use 50 characters or fewer'),
      date: z.string().regex(ISO_DATE, 'Date is required'),
      dueDate: optionalDate,
      status: z.string().nullable(),
      paidDate: optionalDate,
      amount: z
        .union([z.number(), z.literal('')])
        .refine((v) => v !== '', 'Amount is required')
        .refine(maxDp(2), 'Use at most 2 decimal places'),
      gstAmount: z.union([z.number(), z.literal('')]).refine(maxDp(2), 'Use at most 2 decimal places'),
      notes: z.string().max(2000, 'Use 2000 characters or fewer'),
    })
    .superRefine((v, ctx) => {
      const amount: unknown = v.amount;
      if (kind === 'commitment') {
        if (typeof amount === 'number' && amount < 0 && v.type !== 'Variation') {
          ctx.addIssue({ code: 'custom', path: ['amount'], message: 'Only a variation can be negative.' });
        }
        return;
      }
      if (amount === 0) {
        ctx.addIssue({ code: 'custom', path: ['amount'], message: 'Amount cannot be zero' });
      }
      if ((v.type === 'Invoice' || v.type === 'Claim') && v.contractorId === null) {
        ctx.addIssue({ code: 'custom', path: ['contractorId'], message: 'Choose a contractor.' });
      }
      if (v.status === null || !statuses.includes(v.status)) {
        ctx.addIssue({ code: 'custom', path: ['status'], message: 'Choose a status.' });
      }
      if (v.status === 'Paid' && v.paidDate === '') {
        ctx.addIssue({ code: 'custom', path: ['paidDate'], message: 'Enter the paid date.' });
      }
      // yyyy-MM-dd strings order the same as the dates they represent.
      if (ISO_DATE.test(v.dueDate) && ISO_DATE.test(v.date) && v.dueDate < v.date) {
        ctx.addIssue({ code: 'custom', path: ['dueDate'], message: "The due date can't be before the invoice date." });
      }
      const gst: unknown = v.gstAmount;
      if (typeof gst === 'number' && typeof amount === 'number' && Math.sign(gst) === -Math.sign(amount) && gst !== 0) {
        ctx.addIssue({ code: 'custom', path: ['gstAmount'], message: "GST can't be the opposite sign to the amount." });
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
  const status = invoiceStatuses.find((s) => s === v.status) ?? null;
  return {
    ...toCommitmentInput(v),
    type: actualCostTypes.find((t) => t === v.type) ?? null,
    contractorId: v.contractorId,
    // The Payee field is only shown (and only meaningful) when no contractor is chosen.
    payee: v.contractorId === null ? orNull(v.payee) : null,
    dueDate: orNull(v.dueDate),
    status,
    // The server drops a paid date unless the status is Paid; don't send a hidden one.
    paidDate: status === 'Paid' ? orNull(v.paidDate) : null,
    gstAmount: v.gstAmount === '' ? null : v.gstAmount,
  };
}

/**
 * Form values for an existing entry. An older invoice or claim without a contractor is pre-filled with its contract's
 * contractor (when it has a contract), since it must gain one on its next save.
 */
export function fromEntry(
  e: CostReportCommitment['entry'] | CostReportActual['entry'],
  contracts: CostReportContract[] = [],
): CostEntryFormValues {
  const actual = 'status' in e ? e : null;
  const contractContractor = contracts.find((c) => c.id === e.contractId)?.contractorId ?? null;
  return {
    budgetLineId: e.budgetLineId,
    type: e.type,
    contractId: e.contractId,
    contractorId: actual ? (actual.contractorId ?? contractContractor) : null,
    payee: actual?.payee ?? '',
    description: e.description,
    reference: e.reference ?? '',
    date: e.date,
    dueDate: actual?.dueDate ?? '',
    status: actual?.status ?? 'Received',
    paidDate: actual?.paidDate ?? '',
    amount: e.amount,
    gstAmount: actual?.gstAmount ?? '',
    notes: e.notes ?? '',
  };
}
