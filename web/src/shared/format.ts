const DASH = '—';

/** Date-only values (yyyy-MM-dd) are formatted as calendar dates, independent of the browser's time zone. */
export function formatDate(iso: string | null | undefined, culture: string): string {
  if (!iso) return DASH;
  const [year, month, day] = iso.split('-').map(Number);
  return new Intl.DateTimeFormat(culture, { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' }).format(
    new Date(Date.UTC(year, month - 1, day)),
  );
}

export function formatDateTime(iso: string | null | undefined, culture: string, timeZone: string): string {
  if (!iso) return DASH;
  return new Intl.DateTimeFormat(culture, { dateStyle: 'medium', timeStyle: 'short', timeZone }).format(new Date(iso));
}

/** Currency symbol, digit-group separator and decimal separator for a culture/currency, for number-entry inputs. */
export function numberInputParts(
  culture: string,
  currency: string,
): { currencySymbol: string; groupSeparator: string; decimalSeparator: string } {
  const parts = new Intl.NumberFormat(culture, { style: 'currency', currency }).formatToParts(1000.5);
  const currencySymbol = parts.find((part) => part.type === 'currency')?.value ?? '';
  const groupSeparator = parts.find((part) => part.type === 'group')?.value ?? ',';
  const decimalSeparator = parts.find((part) => part.type === 'decimal')?.value ?? '.';
  return { currencySymbol, groupSeparator, decimalSeparator };
}

export function formatMoney(amount: number | null | undefined, currency: string, culture: string): string {
  if (amount === null || amount === undefined) return DASH;
  const fractionDigits = Number.isInteger(amount) ? 0 : 2;
  return new Intl.NumberFormat(culture, {
    style: 'currency',
    currency,
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(amount);
}
