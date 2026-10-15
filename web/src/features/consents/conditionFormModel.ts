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

/** Today's date in the deployment time zone as yyyy-MM-dd (not the browser's zone, not toISOString's UTC). */
export function localToday(timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());
}

export function completeSchema(timeZone: string) {
  return z.object({
    completedOn: z
      .string()
      .min(1, 'Enter the completion date')
      .regex(/^\d{4}-\d{2}-\d{2}$/, 'Enter a valid date')
      .refine((v) => v <= localToday(timeZone), 'Completion date cannot be in the future'),
  });
}
