import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type {
  ActualCostInput,
  ActualCostUpdateInput,
  BudgetLineInput,
  BudgetLineUpdateInput,
  CommitmentInput,
  CommitmentUpdateInput,
  ContractorInvoices,
  ContractorOption,
  ProjectCostReport,
} from './types';

export const costReportKey = (projectId: string) => ['cost-report', projectId];
export const contractorInvoicesKey = (contractorId?: string) =>
  contractorId ? ['contractor-invoices', contractorId] : ['contractor-invoices'];

function useRefreshReport(projectId: string) {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: costReportKey(projectId) }),
      queryClient.invalidateQueries({ queryKey: ['cashflow', projectId] }),
      queryClient.invalidateQueries({ queryKey: contractorInvoicesKey() }),
    ]);
}

export function useCostReport(projectId: string) {
  const api = useApi();
  return useQuery({
    queryKey: costReportKey(projectId),
    queryFn: () =>
      unwrap<ProjectCostReport>(api.GET('/api/costs/projects/{projectId}', { params: { path: { projectId } } })),
  });
}

/**
 * Every contractor, inactive included, for the invoice form's Contractor select (which offers the active ones plus the
 * entry's current one). Keyed under ['contractors'] so the Contractors module's invalidation refreshes it.
 */
export function useContractorOptions() {
  const api = useApi();
  return useQuery({
    queryKey: ['contractors', { includeInactive: true }],
    queryFn: () =>
      unwrap<ContractorOption[]>(api.GET('/api/contractors', { params: { query: { includeInactive: true } } })),
    staleTime: 60_000,
  });
}

/** One contractor's invoices across all projects, with totals and per-contract invoiced-to-date. */
export function useContractorInvoices(contractorId: string) {
  const api = useApi();
  return useQuery({
    queryKey: contractorInvoicesKey(contractorId),
    queryFn: () =>
      unwrap<ContractorInvoices>(
        api.GET('/api/costs/contractors/{contractorId}/invoices', { params: { path: { contractorId } } }),
      ),
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

export function useCreateCommitment(projectId: string) {
  const api = useApi();
  const refresh = useRefreshReport(projectId);
  return useMutation({
    mutationFn: (body: CommitmentInput) =>
      unwrap<unknown>(api.POST('/api/costs/projects/{projectId}/commitments', { params: { path: { projectId } }, body })),
    onSettled: refresh,
  });
}

export function useUpdateCommitment(projectId: string) {
  const api = useApi();
  const refresh = useRefreshReport(projectId);
  return useMutation({
    mutationFn: ({ entryId, body }: { entryId: string; body: CommitmentUpdateInput }) =>
      unwrap<unknown>(
        api.PUT('/api/costs/projects/{projectId}/commitments/{entryId}', { params: { path: { projectId, entryId } }, body }),
      ),
    onSettled: refresh,
  });
}

export function useDeleteCommitment(projectId: string) {
  const api = useApi();
  const refresh = useRefreshReport(projectId);
  return useMutation({
    mutationFn: ({ entryId, version }: { entryId: string; version: number }) =>
      unwrap<void>(
        api.DELETE('/api/costs/projects/{projectId}/commitments/{entryId}', {
          params: { path: { projectId, entryId }, query: { version } },
        }),
      ),
    onSettled: refresh,
  });
}

export function useCreateActualCost(projectId: string) {
  const api = useApi();
  const refresh = useRefreshReport(projectId);
  return useMutation({
    mutationFn: (body: ActualCostInput) =>
      unwrap<unknown>(api.POST('/api/costs/projects/{projectId}/actuals', { params: { path: { projectId } }, body })),
    onSettled: refresh,
  });
}

export function useUpdateActualCost(projectId: string) {
  const api = useApi();
  const refresh = useRefreshReport(projectId);
  return useMutation({
    mutationFn: ({ entryId, body }: { entryId: string; body: ActualCostUpdateInput }) =>
      unwrap<unknown>(
        api.PUT('/api/costs/projects/{projectId}/actuals/{entryId}', { params: { path: { projectId, entryId } }, body }),
      ),
    onSettled: refresh,
  });
}

export function useDeleteActualCost(projectId: string) {
  const api = useApi();
  const refresh = useRefreshReport(projectId);
  return useMutation({
    mutationFn: ({ entryId, version }: { entryId: string; version: number }) =>
      unwrap<void>(
        api.DELETE('/api/costs/projects/{projectId}/actuals/{entryId}', {
          params: { path: { projectId, entryId }, query: { version } },
        }),
      ),
    onSettled: refresh,
  });
}
