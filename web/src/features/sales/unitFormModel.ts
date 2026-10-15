import { z } from 'zod';
import { unitTypes, type UnitDetails, type UnitInput, type UnitType } from './types';

export type UnitFormValues = {
  identifier: string;
  type: UnitType | null;
  bedrooms: number | '';
  bathrooms: number | '';
  carParks: number | '';
  floorAreaSqm: number | '';
  landAreaSqm: number | '';
  listPriceAmount: number | '';
  notes: string;
};

export const emptyUnitValues: UnitFormValues = {
  identifier: '', type: null, bedrooms: '', bathrooms: '', carParks: '',
  floorAreaSqm: '', landAreaSqm: '', listPriceAmount: '', notes: '',
};

const hasAtMostTwoDecimals = (v: number) => Math.abs(v * 100 - Math.round(v * 100)) < 1e-6;
const optional = (inner: z.ZodType<number>) => z.union([z.literal(''), inner], { error: 'Enter a number' });
const roomCount = optional(
  z.number().int('Enter a whole number').min(0, 'Cannot be negative').max(20, 'Use 20 or fewer'),
);
const area = optional(
  z
    .number()
    .min(0, 'Cannot be negative')
    .max(1_000_000, 'Use 1,000,000 m² or less')
    .refine(hasAtMostTwoDecimals, 'Use at most 2 decimal places'),
);

/** Mirrors the API's UnitInputValidator. */
export const unitSchema = z.object({
  identifier: z.string().trim().min(1, 'Identifier is required').max(50, 'Use 50 characters or fewer'),
  type: z.enum(unitTypes, { error: 'Choose a type' }),
  bedrooms: roomCount,
  bathrooms: roomCount,
  carParks: roomCount,
  floorAreaSqm: area,
  landAreaSqm: area,
  listPriceAmount: optional(
    z.number().min(0, 'List price cannot be negative').refine(hasAtMostTwoDecimals, 'Use at most 2 decimal places'),
  ),
  notes: z.string().max(4000, 'Use 4000 characters or fewer'),
});

const orNull = (v: string) => (v.trim() === '' ? null : v.trim());
const numberOrNull = (v: number | '') => (v === '' ? null : Number(v));

export function toUnitInput(v: UnitFormValues): UnitInput {
  return {
    identifier: v.identifier.trim(),
    type: v.type,
    bedrooms: numberOrNull(v.bedrooms),
    bathrooms: numberOrNull(v.bathrooms),
    carParks: numberOrNull(v.carParks),
    floorAreaSqm: numberOrNull(v.floorAreaSqm),
    landAreaSqm: numberOrNull(v.landAreaSqm),
    listPriceAmount: numberOrNull(v.listPriceAmount),
    notes: orNull(v.notes),
  };
}

export function fromUnit(u: UnitDetails): UnitFormValues {
  return {
    identifier: u.identifier,
    type: u.type,
    bedrooms: u.bedrooms ?? '',
    bathrooms: u.bathrooms ?? '',
    carParks: u.carParks ?? '',
    floorAreaSqm: u.floorAreaSqm ?? '',
    landAreaSqm: u.landAreaSqm ?? '',
    listPriceAmount: u.listPriceAmount ?? '',
    notes: u.notes ?? '',
  };
}
