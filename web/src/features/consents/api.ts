import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type {
  ConditionDetails,
  ConditionInput,
  ConditionUpdateInput,
  ConsentDetails,
  ConsentInput,
  ConsentUpdateInput,
  DueItem,
  ReminderPreview,
} from './types';

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

export function useDueItems() {
  const api = useApi();
  return useQuery({
    queryKey: ['consents-due'],
    queryFn: () => unwrap<DueItem[]>(api.GET('/api/consents/due')),
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

export function useCreateCondition(projectId: string) {
  const api = useApi();
  const refresh = useRefreshConsents(projectId);
  return useMutation({
    mutationFn: ({ consentId, body }: { consentId: string; body: ConditionInput }) =>
      unwrap<ConditionDetails>(
        api.POST('/api/consents/projects/{projectId}/consents/{consentId}/conditions', {
          params: { path: { projectId, consentId } },
          body,
        }),
      ),
    onSettled: refresh,
  });
}

export function useUpdateCondition(projectId: string) {
  const api = useApi();
  const refresh = useRefreshConsents(projectId);
  return useMutation({
    mutationFn: ({
      consentId,
      conditionId,
      body,
    }: {
      consentId: string;
      conditionId: string;
      body: ConditionUpdateInput;
    }) =>
      unwrap<ConditionDetails>(
        api.PUT('/api/consents/projects/{projectId}/consents/{consentId}/conditions/{conditionId}', {
          params: { path: { projectId, consentId, conditionId } },
          body,
        }),
      ),
    onSettled: refresh,
  });
}

export function useCompleteCondition(projectId: string) {
  const api = useApi();
  const refresh = useRefreshConsents(projectId);
  return useMutation({
    mutationFn: ({
      consentId,
      conditionId,
      version,
      completedOn,
    }: {
      consentId: string;
      conditionId: string;
      version: number;
      completedOn: string;
    }) =>
      unwrap<ConditionDetails>(
        api.POST(
          '/api/consents/projects/{projectId}/consents/{consentId}/conditions/{conditionId}/complete',
          {
            params: { path: { projectId, consentId, conditionId } },
            body: { version, completedOn },
          },
        ),
      ),
    onSettled: refresh,
  });
}

export function useReopenCondition(projectId: string) {
  const api = useApi();
  const refresh = useRefreshConsents(projectId);
  return useMutation({
    mutationFn: ({
      consentId,
      conditionId,
      version,
    }: {
      consentId: string;
      conditionId: string;
      version: number;
    }) =>
      unwrap<ConditionDetails>(
        api.POST('/api/consents/projects/{projectId}/consents/{consentId}/conditions/{conditionId}/reopen', {
          params: { path: { projectId, consentId, conditionId } },
          body: { version },
        }),
      ),
    onSettled: refresh,
  });
}

export function usePreviewReminders() {
  const api = useApi();
  return useMutation({
    mutationFn: () => unwrap<ReminderPreview>(api.POST('/api/consents/reminders/preview')),
  });
}
