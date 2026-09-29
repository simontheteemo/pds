import { z } from 'zod';
import { stages, statuses, type ProjectDetails, type ProjectInput, type Stage, type Status } from './types';

export type ProjectFormValues = {
  code: string;
  name: string;
  stage: Stage | null;
  status: Status | null;
  plannedStart: string;
  plannedCompletion: string;
  actualStart: string;
  actualCompletion: string;
  budgetAmount: number | string;
  projectManager: string;
  description: string;
  site: {
    addressLine: string;
    suburb: string;
    city: string;
    region: string;
    postcode: string;
    legalDescription: string;
    titleReference: string;
    landAreaSqm: number | string;
  };
};

export const emptyProjectValues: ProjectFormValues = {
  code: '',
  name: '',
  stage: null,
  status: 'OnTrack',
  plannedStart: '',
  plannedCompletion: '',
  actualStart: '',
  actualCompletion: '',
  budgetAmount: '',
  projectManager: '',
  description: '',
  site: { addressLine: '', suburb: '', city: '', region: '', postcode: '', legalDescription: '', titleReference: '', landAreaSqm: '' },
};

const text = (max: number) => z.string().max(max, `Use ${max} characters or fewer`);
const required = (label: string, max: number) =>
  z.string().trim().min(1, `${label} is required`).max(max, `Use ${max} characters or fewer`);
const isoDate = z.string().refine((v) => v === '' || /^\d{4}-\d{2}-\d{2}$/.test(v), 'Enter a valid date');
const hasAtMostTwoDecimals = (v: number) => Math.abs(v * 100 - Math.round(v * 100)) < 1e-6;
const ordered = (start: string, end: string) => !start || !end || end >= start;

/** Mirrors the API validators (ProjectInputValidator) so most mistakes are caught before a round trip. */
export const projectSchema = z
  .object({
    code: z
      .string()
      .trim()
      .min(1, 'Code is required')
      .regex(/^[A-Za-z0-9][A-Za-z0-9-]{0,19}$/, 'Use up to 20 letters, digits or hyphens, starting with a letter or digit'),
    name: required('Name', 200),
    stage: z.enum(stages, { error: 'Choose a stage' }),
    status: z.enum(statuses, { error: 'Choose a status' }),
    plannedStart: isoDate,
    plannedCompletion: isoDate,
    actualStart: isoDate,
    actualCompletion: isoDate,
    budgetAmount: z.union([
      z.literal(''),
      z.number().min(0, 'Budget cannot be negative').refine(hasAtMostTwoDecimals, 'Use at most 2 decimal places'),
    ]),
    projectManager: text(200),
    description: text(4000),
    site: z.object({
      addressLine: required('Address', 200),
      suburb: text(100),
      city: required('City', 100),
      region: text(100),
      postcode: text(20),
      legalDescription: text(500),
      titleReference: text(100),
      landAreaSqm: z.union([z.literal(''), z.number().min(0, 'Land area cannot be negative')]),
    }),
  })
  .refine((v) => ordered(v.plannedStart, v.plannedCompletion), {
    path: ['plannedCompletion'],
    error: 'Planned completion must be on or after the planned start',
  })
  .refine((v) => ordered(v.actualStart, v.actualCompletion), {
    path: ['actualCompletion'],
    error: 'Actual completion must be on or after the actual start',
  });

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());
const numberOrNull = (value: number | string) => (value === '' ? null : Number(value));

export function toProjectInput(v: ProjectFormValues): ProjectInput {
  return {
    code: v.code.trim(),
    name: v.name.trim(),
    stage: v.stage,
    status: v.status,
    plannedStart: orNull(v.plannedStart),
    plannedCompletion: orNull(v.plannedCompletion),
    actualStart: orNull(v.actualStart),
    actualCompletion: orNull(v.actualCompletion),
    budgetAmount: numberOrNull(v.budgetAmount),
    projectManager: orNull(v.projectManager),
    description: orNull(v.description),
    site: {
      addressLine: v.site.addressLine.trim(),
      suburb: orNull(v.site.suburb),
      city: v.site.city.trim(),
      region: orNull(v.site.region),
      postcode: orNull(v.site.postcode),
      legalDescription: orNull(v.site.legalDescription),
      titleReference: orNull(v.site.titleReference),
      landAreaSqm: numberOrNull(v.site.landAreaSqm),
    },
  };
}

export function fromDetails(d: ProjectDetails): ProjectFormValues {
  return {
    code: d.code,
    name: d.name,
    stage: d.stage,
    status: d.status,
    plannedStart: d.plannedStart ?? '',
    plannedCompletion: d.plannedCompletion ?? '',
    actualStart: d.actualStart ?? '',
    actualCompletion: d.actualCompletion ?? '',
    budgetAmount: d.budgetAmount ?? '',
    projectManager: d.projectManager ?? '',
    description: d.description ?? '',
    site: {
      addressLine: d.site.addressLine,
      suburb: d.site.suburb ?? '',
      city: d.site.city,
      region: d.site.region ?? '',
      postcode: d.site.postcode ?? '',
      legalDescription: d.site.legalDescription ?? '',
      titleReference: d.site.titleReference ?? '',
      landAreaSqm: d.site.landAreaSqm ?? '',
    },
  };
}
