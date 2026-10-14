import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type {
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
