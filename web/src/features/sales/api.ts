import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type { ProjectSales, ProjectSalesSummary, SaleInput, UnitDetails, UnitInput, UnitUpdateInput } from './types';

const salesKey = (projectId: string) => ['sales', projectId];
const salesSummaryKey = ['sales-summary'];

function useRefreshSales(projectId: string) {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: salesKey(projectId) }),
      queryClient.invalidateQueries({ queryKey: salesSummaryKey }),
      queryClient.invalidateQueries({ queryKey: ['cashflow', projectId] }),
    ]);
}

export function useProjectSales(projectId: string) {
  const api = useApi();
  return useQuery({
    queryKey: salesKey(projectId),
    queryFn: () =>
      unwrap<ProjectSales>(api.GET('/api/sales/projects/{projectId}/units', { params: { path: { projectId } } })),
  });
}

export function useSalesSummaries() {
  const api = useApi();
  return useQuery({
    queryKey: salesSummaryKey,
    queryFn: () => unwrap<ProjectSalesSummary[]>(api.GET('/api/sales/summary')),
  });
}

export function useCreateUnit(projectId: string) {
  const api = useApi();
  const refresh = useRefreshSales(projectId);
  return useMutation({
    mutationFn: (body: UnitInput) =>
      unwrap<UnitDetails>(api.POST('/api/sales/projects/{projectId}/units', { params: { path: { projectId } }, body })),
    onSettled: refresh,
  });
}

export function useUpdateUnit(projectId: string) {
  const api = useApi();
  const refresh = useRefreshSales(projectId);
  return useMutation({
    mutationFn: ({ unitId, body }: { unitId: string; body: UnitUpdateInput }) =>
      unwrap<UnitDetails>(
        api.PUT('/api/sales/projects/{projectId}/units/{unitId}', { params: { path: { projectId, unitId } }, body }),
      ),
    onSettled: refresh,
  });
}

export function useUpdateSale(projectId: string) {
  const api = useApi();
  const refresh = useRefreshSales(projectId);
  return useMutation({
    mutationFn: ({ unitId, body }: { unitId: string; body: SaleInput }) =>
      unwrap<UnitDetails>(
        api.PUT('/api/sales/projects/{projectId}/units/{unitId}/sale', { params: { path: { projectId, unitId } }, body }),
      ),
    onSettled: refresh,
  });
}
