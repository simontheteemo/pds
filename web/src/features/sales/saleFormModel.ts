import { z } from 'zod';
import {
  hasSale,
  requiresAgreement,
  requiresSettlement,
  requiresUnconditional,
  saleStatuses,
  type SaleInput,
  type SaleStatus,
  type UnitDetails,
} from './types';

export type SaleFormValues = {
  status: SaleStatus;
  buyerName: string;
  buyerEmail: string;
  buyerPhone: string;
  buyerSolicitor: string;
  agreementDate: string;
  unconditionalDate: string;
  settlementDate: string;
  salePriceAmount: number | '';
  depositAmount: number | '';
};

const NAME_MAX = 200;
const EMAIL_MAX = 200;
const PHONE_MAX = 50;

const hasAtMostTwoDecimals = (v: number) => Math.abs(v * 100 - Math.round(v * 100)) < 1e-6;
/** FluentValidation's EmailAddress check: one '@' that is neither the first nor the last character. */
const looksLikeEmail = (v: string) => {
  const at = v.indexOf('@');
  return at > 0 && at === v.lastIndexOf('@') && at < v.length - 1;
};
/** Mirrors SaleRules.IsOrdered: either side missing counts as ordered. */
const isOrdered = (earlier: string, later: string) => earlier === '' || later === '' || later >= earlier;

const amount = z.union([z.literal(''), z.number()], { error: 'Enter a number' });

/**
 * Mirrors the API's UpdateSaleValidator: sale fields are only checked when the status carries a sale
 * (anything other than Available and Withdrawn); the server ignores them otherwise.
 */
export const saleSchema = z
  .object({
    status: z.enum(saleStatuses, { error: 'Choose a status.' }),
    buyerName: z.string(),
    buyerEmail: z.string(),
    buyerPhone: z.string(),
    buyerSolicitor: z.string(),
    agreementDate: z.string(),
    unconditionalDate: z.string(),
    settlementDate: z.string(),
    salePriceAmount: amount,
    depositAmount: amount,
  })
  .superRefine((v, ctx) => {
    if (!hasSale(v.status)) return;
    const issue = (path: keyof SaleFormValues, message: string) => ctx.addIssue({ code: 'custom', path: [path], message });

    const buyerName = v.buyerName.trim();
    if (buyerName === '') issue('buyerName', "Enter the buyer's name.");
    else if (buyerName.length > NAME_MAX) issue('buyerName', `Use ${NAME_MAX} characters or fewer.`);

    const email = v.buyerEmail.trim();
    if (email !== '' && !looksLikeEmail(email)) issue('buyerEmail', 'Enter a valid email address.');
    else if (email.length > EMAIL_MAX) issue('buyerEmail', `Use ${EMAIL_MAX} characters or fewer.`);
    if (v.buyerPhone.trim().length > PHONE_MAX) issue('buyerPhone', `Use ${PHONE_MAX} characters or fewer.`);
    if (v.buyerSolicitor.trim().length > NAME_MAX) issue('buyerSolicitor', `Use ${NAME_MAX} characters or fewer.`);

    if (requiresAgreement(v.status)) {
      if (v.agreementDate === '') issue('agreementDate', 'Enter the S&P agreement date.');
      if (v.salePriceAmount === '') issue('salePriceAmount', 'Enter the sale price.');
    }
    if (v.salePriceAmount !== '') {
      if (v.salePriceAmount < 0) issue('salePriceAmount', 'Sale price cannot be negative.');
      else if (!hasAtMostTwoDecimals(v.salePriceAmount)) issue('salePriceAmount', 'Use at most 2 decimal places.');
    }
    if (v.depositAmount !== '') {
      if (v.depositAmount < 0) issue('depositAmount', 'Deposit cannot be negative.');
      else if (!hasAtMostTwoDecimals(v.depositAmount)) issue('depositAmount', 'Use at most 2 decimal places.');
      else if (v.salePriceAmount !== '' && v.depositAmount > v.salePriceAmount)
        issue('depositAmount', "Deposit can't be more than the sale price.");
    }

    if (requiresUnconditional(v.status) && v.unconditionalDate === '')
      issue('unconditionalDate', 'Enter the unconditional date.');
    else if (!isOrdered(v.agreementDate, v.unconditionalDate))
      issue('unconditionalDate', 'Unconditional date must be on or after the agreement date.');

    if (requiresSettlement(v.status) && v.settlementDate === '') issue('settlementDate', 'Enter the settlement date.');
    else if (!isOrdered(v.agreementDate, v.settlementDate) || !isOrdered(v.unconditionalDate, v.settlementDate))
      issue('settlementDate', 'Settlement date must be on or after the agreement and unconditional dates.');
  });

const orNull = (v: string) => (v.trim() === '' ? null : v.trim());
const numberOrNull = (v: number | '') => (v === '' ? null : Number(v));

export function toSaleInput(v: SaleFormValues, version: number): SaleInput {
  if (!hasSale(v.status)) {
    return {
      status: v.status,
      buyerName: null,
      buyerEmail: null,
      buyerPhone: null,
      buyerSolicitor: null,
      agreementDate: null,
      salePriceAmount: null,
      depositAmount: null,
      unconditionalDate: null,
      settlementDate: null,
      version,
    };
  }
  return {
    status: v.status,
    buyerName: orNull(v.buyerName),
    buyerEmail: orNull(v.buyerEmail),
    buyerPhone: orNull(v.buyerPhone),
    buyerSolicitor: orNull(v.buyerSolicitor),
    agreementDate: orNull(v.agreementDate),
    salePriceAmount: numberOrNull(v.salePriceAmount),
    depositAmount: numberOrNull(v.depositAmount),
    unconditionalDate: orNull(v.unconditionalDate),
    settlementDate: orNull(v.settlementDate),
    version,
  };
}

export function fromUnit(u: UnitDetails): SaleFormValues {
  return {
    status: u.status,
    buyerName: u.buyerName ?? '',
    buyerEmail: u.buyerEmail ?? '',
    buyerPhone: u.buyerPhone ?? '',
    buyerSolicitor: u.buyerSolicitor ?? '',
    agreementDate: u.agreementDate ?? '',
    unconditionalDate: u.unconditionalDate ?? '',
    settlementDate: u.settlementDate ?? '',
    salePriceAmount: u.salePriceAmount ?? '',
    depositAmount: u.depositAmount ?? '',
  };
}
