import { z } from 'zod';
import { contractorTypes, type ContractorDetails, type ContractorInput, type ContractorType } from './types';

export type ContractorFormValues = {
  name: string;
  type: ContractorType | null;
  trade: string;
  nzbn: string;
  contactName: string;
  email: string;
  phone: string;
  address: string;
  publicLiabilityExpiry: string;
  professionalIndemnityExpiry: string;
  licenceNumber: string;
  licenceExpiry: string;
  notes: string;
};

export const emptyContractorValues: ContractorFormValues = {
  name: '',
  type: null,
  trade: '',
  nzbn: '',
  contactName: '',
  email: '',
  phone: '',
  address: '',
  publicLiabilityExpiry: '',
  professionalIndemnityExpiry: '',
  licenceNumber: '',
  licenceExpiry: '',
  notes: '',
};

const text = (max: number) => z.string().max(max, `Use ${max} characters or fewer`);
const isoDate = z.string().refine((v) => v === '' || /^\d{4}-\d{2}-\d{2}$/.test(v), 'Enter a valid date');

/** Mirrors the API validators (ContractorInputValidator) so most mistakes are caught before a round trip. */
export const contractorSchema = z.object({
  name: z.string().trim().min(1, 'Name is required').max(200, 'Use 200 characters or fewer'),
  type: z.enum(contractorTypes, { error: 'Choose a type' }),
  trade: text(100),
  nzbn: z.string().trim().refine((v) => v === '' || /^\d{13}$/.test(v), 'NZBN must be 13 digits'),
  contactName: text(200),
  email: text(200).refine((v) => v.trim() === '' || z.email().safeParse(v.trim()).success, 'Enter a valid email address'),
  phone: text(50),
  address: text(300),
  publicLiabilityExpiry: isoDate,
  professionalIndemnityExpiry: isoDate,
  licenceNumber: text(50),
  licenceExpiry: isoDate,
  notes: text(4000),
});

const orNull = (value: string) => (value.trim() === '' ? null : value.trim());

export function toContractorInput(v: ContractorFormValues): ContractorInput {
  return {
    name: v.name.trim(),
    type: v.type,
    trade: orNull(v.trade),
    nzbn: orNull(v.nzbn),
    contactName: orNull(v.contactName),
    email: orNull(v.email),
    phone: orNull(v.phone),
    address: orNull(v.address),
    publicLiabilityExpiry: orNull(v.publicLiabilityExpiry),
    professionalIndemnityExpiry: orNull(v.professionalIndemnityExpiry),
    licenceNumber: orNull(v.licenceNumber),
    licenceExpiry: orNull(v.licenceExpiry),
    notes: orNull(v.notes),
  };
}

export function fromContractor(d: ContractorDetails): ContractorFormValues {
  return {
    name: d.name,
    type: d.type,
    trade: d.trade ?? '',
    nzbn: d.nzbn ?? '',
    contactName: d.contactName ?? '',
    email: d.email ?? '',
    phone: d.phone ?? '',
    address: d.address ?? '',
    publicLiabilityExpiry: d.publicLiabilityExpiry ?? '',
    professionalIndemnityExpiry: d.professionalIndemnityExpiry ?? '',
    licenceNumber: d.licenceNumber ?? '',
    licenceExpiry: d.licenceExpiry ?? '',
    notes: d.notes ?? '',
  };
}
