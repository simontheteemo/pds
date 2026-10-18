import { z } from 'zod';
import { milestoneCategories, type MilestoneCategory, type MilestoneDetails, type MilestoneInput } from './types';

export type MilestoneFormValues = {
  name: string;
  category: MilestoneCategory | '';
  plannedDate: string;
  forecastDate: string;
  actualDate: string;
  owner: string;
  sortOrder: number | '';
  notes: string;
};

export function emptyMilestoneValues(existing: MilestoneDetails[]): MilestoneFormValues {
  const max = existing.reduce((m, x) => Math.max(m, x.sortOrder), 0);
  return {
    name: '',
    category: '',
    plannedDate: '',
    forecastDate: '',
    actualDate: '',
    owner: '',
    sortOrder: Math.min(max + 10, 9999),
    notes: '',
  };
}

const optionalDate = z.string().regex(/^(\d{4}-\d{2}-\d{2})?$/, 'Enter a valid date');

/** Mirrors the API's milestone validators. */
export const milestoneSchema = z.object({
  name: z.string().trim().min(1, 'Name is required').max(100, 'Use 100 characters or fewer'),
  category: z.string().refine((v) => (milestoneCategories as readonly string[]).includes(v), 'Category is required'),
  plannedDate: optionalDate,
  forecastDate: optionalDate,
  actualDate: optionalDate,
  owner: z.string().trim().max(200, 'Use 200 characters or fewer'),
  sortOrder: z
    .union([z.number(), z.literal('')])
    .refine((v) => v !== '' && Number.isInteger(v) && v >= 0 && v <= 9999, 'Enter a whole number from 0 to 9999'),
  notes: z.string().max(2000, 'Use 2000 characters or fewer'),
});

const orNull = (s: string) => (s.trim() === '' ? null : s.trim());

export function toMilestoneInput(v: MilestoneFormValues): MilestoneInput {
  return {
    name: v.name.trim(),
    category: v.category === '' ? null : v.category,
    plannedDate: orNull(v.plannedDate),
    forecastDate: orNull(v.forecastDate),
    actualDate: orNull(v.actualDate),
    owner: orNull(v.owner),
    sortOrder: v.sortOrder === '' ? null : v.sortOrder,
    notes: orNull(v.notes),
  };
}

export function fromMilestone(m: MilestoneDetails): MilestoneFormValues {
  return {
    name: m.name,
    category: m.category,
    plannedDate: m.plannedDate ?? '',
    forecastDate: m.forecastDate ?? '',
    actualDate: m.actualDate ?? '',
    owner: m.owner ?? '',
    sortOrder: m.sortOrder,
    notes: m.notes ?? '',
  };
}
