import { z } from 'zod';
import {
  consentStatuses,
  consentTypes,
  type ConsentDetails,
  type ConsentInput,
  type ConsentStatus,
  type ConsentType,
} from './types';

export type ConsentFormValues = {
  type: ConsentType | null;
  title: string;
  authority: string;
  reference: string;
  status: ConsentStatus;
  lodgedDate: string;
  grantedDate: string;
  expiryDate: string;
  notes: string;
};

export const emptyConsentValues: ConsentFormValues = {
  type: null,
  title: '',
  authority: '',
  reference: '',
  status: 'Preparing',
  lodgedDate: '',
  grantedDate: '',
  expiryDate: '',
  notes: '',
};

const dateField = z.string().refine((v) => v === '' || /^\d{4}-\d{2}-\d{2}$/.test(v), 'Enter a valid date');

/** Mirrors the API's consent input validator. */
export const consentSchema = z
  .object({
    type: z.enum(consentTypes, { error: 'Choose a type' }),
    title: z.string().trim().min(1, 'Title is required').max(200, 'Use 200 characters or fewer'),
    authority: z.string().max(200, 'Use 200 characters or fewer'),
    reference: z.string().max(100, 'Use 100 characters or fewer'),
    status: z.enum(consentStatuses),
    lodgedDate: dateField,
    grantedDate: dateField,
    expiryDate: dateField,
    notes: z.string().max(4000, 'Use 4000 characters or fewer'),
  })
  .superRefine((v, ctx) => {
    if (v.status === 'Granted' && v.grantedDate === '') {
      ctx.addIssue({
        code: 'custom',
        path: ['grantedDate'],
        message: 'Enter the date the consent was granted',
      });
    }
    if (v.grantedDate && v.lodgedDate && v.grantedDate < v.lodgedDate) {
      ctx.addIssue({
        code: 'custom',
        path: ['grantedDate'],
        message: 'Granted date cannot be before the lodged date',
      });
    }
    if (v.expiryDate && v.grantedDate && v.expiryDate < v.grantedDate) {
      ctx.addIssue({
        code: 'custom',
        path: ['expiryDate'],
        message: 'Expiry date cannot be before the granted date',
      });
    }
  });

const orNull = (v: string) => (v.trim() === '' ? null : v.trim());

export function toConsentInput(v: ConsentFormValues): ConsentInput {
  return {
    type: v.type,
    title: v.title.trim(),
    authority: orNull(v.authority),
    reference: orNull(v.reference),
    status: v.status,
    lodgedDate: orNull(v.lodgedDate),
    grantedDate: orNull(v.grantedDate),
    expiryDate: orNull(v.expiryDate),
    notes: orNull(v.notes),
  };
}

export function fromConsent(c: ConsentDetails): ConsentFormValues {
  return {
    type: c.type,
    title: c.title,
    authority: c.authority ?? '',
    reference: c.reference ?? '',
    status: c.status,
    lodgedDate: c.lodgedDate ?? '',
    grantedDate: c.grantedDate ?? '',
    expiryDate: c.expiryDate ?? '',
    notes: c.notes ?? '',
  };
}
