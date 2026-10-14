import { z } from 'zod';
import type { ConditionDetails, ConditionInput } from './types';

export type ConditionFormValues = {
  reference: string;
  description: string;
  dueDate: string;
};

export const emptyConditionValues: ConditionFormValues = { reference: '', description: '', dueDate: '' };

/** Mirrors the API's condition input validator. */
export const conditionSchema = z.object({
  reference: z.string().max(50, 'Use 50 characters or fewer'),
  description: z.string().trim().min(1, 'Describe the condition').max(2000, 'Use 2000 characters or fewer'),
  dueDate: z.string().refine((v) => v === '' || /^\d{4}-\d{2}-\d{2}$/.test(v), 'Enter a valid date'),
});

const orNull = (v: string) => (v.trim() === '' ? null : v.trim());

export function toConditionInput(v: ConditionFormValues): ConditionInput {
  return {
    reference: orNull(v.reference),
    description: v.description.trim(),
    dueDate: orNull(v.dueDate),
  };
}

export function fromCondition(c: ConditionDetails): ConditionFormValues {
  return { reference: c.reference ?? '', description: c.description, dueDate: c.dueDate ?? '' };
}

/** Today's date in the browser's local time zone as yyyy-MM-dd (not toISOString, which is UTC). */
export function localToday(): string {
  const d = new Date();
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

export const completeSchema = z.object({
  completedOn: z
    .string()
    .min(1, 'Enter the completion date')
    .regex(/^\d{4}-\d{2}-\d{2}$/, 'Enter a valid date')
    .refine((v) => v <= localToday(), 'Completion date cannot be in the future'),
});
