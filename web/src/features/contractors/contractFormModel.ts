import { z } from 'zod';
import {
  contractForms,
  contractStatuses,
  type ContractDetails,
  type ContractForm,
  type ContractInput,
  type ContractStatus,
} from './types';

export type ContractFormValues = {
  contractorId: string | null;
  title: string;
  reference: string;
  form: ContractForm | null;
  valueAmount: number | '';
  startDate: string;
  endDate: string;
  retentionPercent: number | '';
  status: ContractStatus;
  notes: string;
};

export const emptyContractValues: ContractFormValues = {
  contractorId: null,
  title: '',
  reference: '',
  form: null,
  valueAmount: '',
  startDate: '',
  endDate: '',
  retentionPercent: '',
  status: 'Draft',
  notes: '',
};

const isoDate = z.string().refine((v) => v === '' || /^\d{4}-\d{2}-\d{2}$/.test(v), 'Enter a valid date');
const maxDp = (dp: number) => (v: number | '') =>
  v === '' || Math.abs(Math.round(v * 10 ** dp) - v * 10 ** dp) < 1e-6;

/** Mirrors the API's contract validators. */
export const contractSchema = z
  .object({
    contractorId: z.string({ error: 'Choose a contractor' }).min(1, 'Choose a contractor'),
    title: z.string().trim().min(1, 'Title is required').max(200, 'Use 200 characters or fewer'),
    reference: z.string().max(50, 'Use 50 characters or fewer'),
    form: z.enum(contractForms, { error: 'Choose a contract form.' }),
    valueAmount: z
      .union([z.number().min(0, 'Value must be 0 or more'), z.literal('')])
      .refine(maxDp(2), 'Use at most 2 decimal places'),
    startDate: isoDate,
    endDate: isoDate,
    retentionPercent: z
      .union([z.number().min(0, 'Retention must be between 0 and 20').max(20, 'Retention must be between 0 and 20'), z.literal('')])
      .refine(maxDp(1), 'Use at most 1 decimal place'),
    status: z.enum(contractStatuses),
    notes: z.string().max(4000, 'Use 4000 characters or fewer'),
  })
  .superRefine((v, ctx) => {
    if (v.startDate && v.endDate && v.endDate < v.startDate) {
      ctx.addIssue({ code: 'custom', path: ['endDate'], message: 'End date must be on or after the start date' });
    }
  });

const orNull = (v: string) => (v.trim() === '' ? null : v.trim());

export function toContractInput(v: ContractFormValues): ContractInput {
  return {
    contractorId: v.contractorId,
    title: v.title.trim(),
    reference: orNull(v.reference),
    form: v.form,
    valueAmount: v.valueAmount === '' ? null : v.valueAmount,
    startDate: orNull(v.startDate),
    endDate: orNull(v.endDate),
    retentionPercent: v.retentionPercent === '' ? null : v.retentionPercent,
    status: v.status,
    notes: orNull(v.notes),
  };
}

export function fromContract(c: ContractDetails): ContractFormValues {
  return {
    contractorId: c.contractorId,
    title: c.title,
    reference: c.reference ?? '',
    form: c.form,
    valueAmount: c.valueAmount ?? '',
    startDate: c.startDate ?? '',
    endDate: c.endDate ?? '',
    retentionPercent: c.retentionPercent ?? '',
    status: c.status,
    notes: c.notes ?? '',
  };
}
