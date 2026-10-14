import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type {
  ContractDetails,
  ContractInput,
  ContractUpdateInput,
  ProjectContract,
  ContractorContract,
  ContractorDetails,
  ContractorInput,
  ContractorListItem,
  ContractorListQuery,
  ContractorUpdateInput,
} from './types';

function useStoreContractor() {
  const queryClient = useQueryClient();
  return (contractor: ContractorDetails) => {
    queryClient.setQueryData(['contractor', contractor.id], contractor);
    void queryClient.invalidateQueries({ queryKey: ['contractors'] });
  };
}

export function useContractors(query: ContractorListQuery) {
  const api = useApi();
  return useQuery({
    queryKey: ['contractors', query],
    queryFn: () => unwrap<ContractorListItem[]>(api.GET('/api/contractors', { params: { query } })),
    placeholderData: keepPreviousData,
  });
}

export function useContractor(id: string) {
  const api = useApi();
  return useQuery({
    queryKey: ['contractor', id],
    queryFn: () =>
      unwrap<ContractorDetails>(api.GET('/api/contractors/{contractorId}', { params: { path: { contractorId: id } } })),
  });
}

export function useContractorContracts(id: string) {
  const api = useApi();
  return useQuery({
    queryKey: ['contractor-contracts', id],
    queryFn: () =>
      unwrap<ContractorContract[]>(
        api.GET('/api/contractors/{contractorId}/contracts', { params: { path: { contractorId: id } } }),
      ),
  });
}

export function useCreateContractor() {
  const api = useApi();
  const store = useStoreContractor();
  return useMutation({
    mutationFn: (body: ContractorInput) => unwrap<ContractorDetails>(api.POST('/api/contractors', { body })),
    onSuccess: store,
  });
}

export function useUpdateContractor(id: string) {
  const api = useApi();
  const store = useStoreContractor();
  return useMutation({
    mutationFn: (body: ContractorUpdateInput) =>
      unwrap<ContractorDetails>(
        api.PUT('/api/contractors/{contractorId}', { params: { path: { contractorId: id } }, body }),
      ),
    onSuccess: store,
  });
}

export function useSetContractorActive(id: string) {
  const api = useApi();
  const store = useStoreContractor();
  return useMutation({
    mutationFn: ({ active, version }: { active: boolean; version: number }) =>
      unwrap<ContractorDetails>(
        active
          ? api.POST('/api/contractors/{contractorId}/activate', { params: { path: { contractorId: id } }, body: { version } })
          : api.POST('/api/contractors/{contractorId}/deactivate', { params: { path: { contractorId: id } }, body: { version } }),
      ),
    onSuccess: store,
  });
}

const projectContractsKey = (projectId: string) => ['project-contracts', projectId];

function useRefreshContracts(projectId: string) {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: projectContractsKey(projectId) }),
      queryClient.invalidateQueries({ queryKey: ['contractor-contracts'] }),
    ]);
}

export function useProjectContracts(projectId: string) {
  const api = useApi();
  return useQuery({
    queryKey: projectContractsKey(projectId),
    queryFn: () =>
      unwrap<ProjectContract[]>(
        api.GET('/api/contractors/projects/{projectId}/contracts', { params: { path: { projectId } } }),
      ),
  });
}

export function useProjectContract(projectId: string, contractId: string) {
  const api = useApi();
  return useQuery({
    queryKey: ['project-contract', projectId, contractId],
    queryFn: () =>
      unwrap<ContractDetails>(
        api.GET('/api/contractors/projects/{projectId}/contracts/{contractId}', {
          params: { path: { projectId, contractId } },
        }),
      ),
    gcTime: 0,
  });
}

export function useCreateContract(projectId: string) {
  const api = useApi();
  const refresh = useRefreshContracts(projectId);
  return useMutation({
    mutationFn: (body: ContractInput) =>
      unwrap<ContractDetails>(
        api.POST('/api/contractors/projects/{projectId}/contracts', { params: { path: { projectId } }, body }),
      ),
    onSettled: refresh,
  });
}

export function useUpdateContract(projectId: string) {
  const api = useApi();
  const refresh = useRefreshContracts(projectId);
  return useMutation({
    mutationFn: ({ contractId, body }: { contractId: string; body: ContractUpdateInput }) =>
      unwrap<ContractDetails>(
        api.PUT('/api/contractors/projects/{projectId}/contracts/{contractId}', {
          params: { path: { projectId, contractId } },
          body,
        }),
      ),
    onSettled: refresh,
  });
}
