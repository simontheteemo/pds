import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type { BudgetLineInput, BudgetLineUpdateInput, ProjectCostReport } from './types';

export const costReportKey = (projectId: string) => ['cost-report', projectId];

function useRefreshReport(projectId: string) {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: costReportKey(projectId) });
}

export function useCostReport(projectId: string) {
  const api = useApi();
  return useQuery({
    queryKey: costReportKey(projectId),
    queryFn: () =>
      unwrap<ProjectCostReport>(api.GET('/api/costs/projects/{projectId}', { params: { path: { projectId } } })),
  });
}

export function useCreateBudgetLine(projectId: string) {
  const api = useApi();
  const refresh = useRefreshReport(projectId);
  return useMutation({
    mutationFn: (body: BudgetLineInput) =>
      unwrap<unknown>(api.POST('/api/costs/projects/{projectId}/lines', { params: { path: { projectId } }, body })),
    onSettled: refresh,
  });
}

export function useAddStandardLines(projectId: string) {
  const api = useApi();
  const refresh = useRefreshReport(projectId);
  return useMutation({
    mutationFn: () =>
      unwrap<unknown[]>(api.POST('/api/costs/projects/{projectId}/lines/standard', { params: { path: { projectId } } })),
    onSettled: refresh,
  });
}

export function useUpdateBudgetLine(projectId: string) {
  const api = useApi();
  const refresh = useRefreshReport(projectId);
  return useMutation({
    mutationFn: ({ lineId, body }: { lineId: string; body: BudgetLineUpdateInput }) =>
      unwrap<unknown>(
        api.PUT('/api/costs/projects/{projectId}/lines/{lineId}', { params: { path: { projectId, lineId } }, body }),
      ),
    onSettled: refresh,
  });
}

export function useDeleteBudgetLine(projectId: string) {
  const api = useApi();
  const refresh = useRefreshReport(projectId);
  return useMutation({
    mutationFn: ({ lineId, version }: { lineId: string; version: number }) =>
      unwrap<void>(
        api.DELETE('/api/costs/projects/{projectId}/lines/{lineId}', {
          params: { path: { projectId, lineId }, query: { version } },
        }),
      ),
    onSettled: refresh,
  });
}
