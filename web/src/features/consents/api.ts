import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type { ConsentDetails, ConsentInput, ConsentUpdateInput } from './types';

const consentsKey = (projectId: string) => ['consents', projectId];

function useRefreshConsents(projectId: string) {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: consentsKey(projectId) }),
      queryClient.invalidateQueries({ queryKey: ['consents-due'] }),
    ]);
}

export function useConsents(projectId: string) {
  const api = useApi();
  return useQuery({
    queryKey: consentsKey(projectId),
    queryFn: () =>
      unwrap<ConsentDetails[]>(
        api.GET('/api/consents/projects/{projectId}/consents', { params: { path: { projectId } } }),
      ),
  });
}

export function useCreateConsent(projectId: string) {
  const api = useApi();
  const refresh = useRefreshConsents(projectId);
  return useMutation({
    mutationFn: (body: ConsentInput) =>
      unwrap<ConsentDetails>(
        api.POST('/api/consents/projects/{projectId}/consents', { params: { path: { projectId } }, body }),
      ),
    onSettled: refresh,
  });
}

export function useUpdateConsent(projectId: string) {
  const api = useApi();
  const refresh = useRefreshConsents(projectId);
  return useMutation({
    mutationFn: ({ consentId, body }: { consentId: string; body: ConsentUpdateInput }) =>
      unwrap<ConsentDetails>(
        api.PUT('/api/consents/projects/{projectId}/consents/{consentId}', {
          params: { path: { projectId, consentId } },
          body,
        }),
      ),
    onSettled: refresh,
  });
}
