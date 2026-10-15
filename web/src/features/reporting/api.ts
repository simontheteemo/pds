import { useQuery } from '@tanstack/react-query';
import { ApiError, unwrap, useApi, type Api, type ProblemDetails } from '../../shared/api/client';
import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

export type ExportDataset = DeepRequired<
  paths['/api/reporting/exports']['get']['responses'][200]['content']['application/json']
>[number];

export function useExports(enabled: boolean) {
  const api = useApi();
  return useQuery({
    queryKey: ['reporting', 'exports'],
    queryFn: () => unwrap<ExportDataset[]>(api.GET('/api/reporting/exports')),
    enabled,
  });
}

function today(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** Fetches the CSV with the bearer token and saves it through a temporary link. */
export async function downloadExport(api: Api, name: string): Promise<void> {
  const { data, error, response } = await api.GET('/api/reporting/exports/{name}', {
    params: { path: { name } },
    parseAs: 'blob',
  });
  if (!response.ok || !data) throw new ApiError(response.status, (error ?? undefined) as ProblemDetails | undefined);
  const url = URL.createObjectURL(data as unknown as Blob);
  try {
    const a = document.createElement('a');
    a.href = url;
    a.download = `pds-${name}-${today()}.csv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
  } finally {
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }
}
