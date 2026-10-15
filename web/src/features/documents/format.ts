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

/** Client-side file checks mirroring the server settings; returns a message, or null when the file is acceptable. */
export function validateFile(
  file: File | null,
  settings: { allowedExtensions: string[]; maxFileSizeBytes: number },
  culture: string,
): string | null {
  if (!file) return 'Choose a file';
  const ext = file.name.includes('.') ? file.name.slice(file.name.lastIndexOf('.') + 1).toLowerCase() : '';
  if (!settings.allowedExtensions.some((e) => e.replace(/^\./, '').toLowerCase() === ext))
    return "This file type isn't allowed";
  if (file.size === 0) return 'The file is empty';
  if (file.size > settings.maxFileSizeBytes) {
    return `The file is larger than the ${formatBytes(settings.maxFileSizeBytes, culture)} limit`;
  }
  return null;
}
