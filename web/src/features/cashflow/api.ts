import { useQuery } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type { ProjectCashflow } from './types';

export const cashflowKey = (projectId: string) => ['cashflow', projectId];

export function useProjectCashflow(projectId: string) {
  const api = useApi();
  return useQuery({
    queryKey: cashflowKey(projectId),
    queryFn: () =>
      unwrap<ProjectCashflow>(api.GET('/api/cashflow/projects/{projectId}', { params: { path: { projectId } } })),
  });
}
