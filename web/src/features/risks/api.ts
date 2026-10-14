import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type { RiskDetails, RiskInput, RiskUpdateInput } from './types';

const risksKey = (projectId: string) => ['risks', projectId];

function useRefreshRisks(projectId: string) {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: risksKey(projectId) });
}

export function useRisks(projectId: string, includeClosed: boolean) {
  const api = useApi();
  return useQuery({
    queryKey: [...risksKey(projectId), { includeClosed }],
    queryFn: () =>
      unwrap<RiskDetails[]>(
        api.GET('/api/risks/projects/{projectId}/risks', { params: { path: { projectId }, query: { includeClosed } } }),
      ),
  });
}

export function useCreateRisk(projectId: string) {
  const api = useApi();
  const refresh = useRefreshRisks(projectId);
  return useMutation({
    mutationFn: (body: RiskInput) =>
      unwrap<RiskDetails>(api.POST('/api/risks/projects/{projectId}/risks', { params: { path: { projectId } }, body })),
    onSettled: refresh,
  });
}

export function useUpdateRisk(projectId: string) {
  const api = useApi();
  const refresh = useRefreshRisks(projectId);
  return useMutation({
    mutationFn: ({ riskId, body }: { riskId: string; body: RiskUpdateInput }) =>
      unwrap<RiskDetails>(
        api.PUT('/api/risks/projects/{projectId}/risks/{riskId}', { params: { path: { projectId, riskId } }, body }),
      ),
    onSettled: refresh,
  });
}

export function useCloseRisk(projectId: string) {
  const api = useApi();
  const refresh = useRefreshRisks(projectId);
  return useMutation({
    mutationFn: ({ riskId, version, note }: { riskId: string; version: number; note: string }) =>
      unwrap<RiskDetails>(
        api.POST('/api/risks/projects/{projectId}/risks/{riskId}/close', {
          params: { path: { projectId, riskId } },
          body: { version, note },
        }),
      ),
    onSettled: refresh,
  });
}

export function useReopenRisk(projectId: string) {
  const api = useApi();
  const refresh = useRefreshRisks(projectId);
  return useMutation({
    mutationFn: ({ riskId, version }: { riskId: string; version: number }) =>
      unwrap<RiskDetails>(
        api.POST('/api/risks/projects/{projectId}/risks/{riskId}/reopen', {
          params: { path: { projectId, riskId } },
          body: { version },
        }),
      ),
    onSettled: refresh,
  });
}
