const UNITS = ['KB', 'MB', 'GB'] as const;

export function formatBytes(bytes: number, culture: string): string {
  if (bytes < 1024) return `${bytes} B`;
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < UNITS.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${new Intl.NumberFormat(culture, { maximumFractionDigits: 1 }).format(value)} ${UNITS[unit]}`;
}
