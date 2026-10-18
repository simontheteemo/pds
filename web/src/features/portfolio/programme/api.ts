import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../../shared/api/client';
import { dueDate, isSlipping, type MilestoneDetails, type MilestoneInput, type MilestoneList, type MilestoneUpdateInput } from './types';

export const milestonesKey = (projectId: string) => ['milestones', projectId];

function useRefresh(projectId: string) {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: milestonesKey(projectId) }),
      queryClient.invalidateQueries({ queryKey: ['projects'] }),
      queryClient.invalidateQueries({ queryKey: ['portfolio-summary'] }),
    ]);
}

export function useMilestones(projectId: string) {
  const api = useApi();
  return useQuery({
    queryKey: milestonesKey(projectId),
    queryFn: () =>
      unwrap<MilestoneList>(api.GET('/api/portfolio/projects/{id}/milestones', { params: { path: { id: projectId } } })),
  });
}

export function useCreateMilestone(projectId: string) {
  const api = useApi();
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: (body: MilestoneInput) =>
      unwrap<unknown>(api.POST('/api/portfolio/projects/{id}/milestones', { params: { path: { id: projectId } }, body })),
    onSettled: refresh,
  });
}

export function useUpdateMilestone(projectId: string) {
  const api = useApi();
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: ({ milestoneId, body }: { milestoneId: string; body: MilestoneUpdateInput }) =>
      unwrap<unknown>(
        api.PUT('/api/portfolio/projects/{id}/milestones/{milestoneId}', {
          params: { path: { id: projectId, milestoneId } },
          body,
        }),
      ),
    onSettled: refresh,
  });
}

export function useDeleteMilestone(projectId: string) {
  const api = useApi();
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: ({ milestoneId, version }: { milestoneId: string; version: number }) =>
      unwrap<void>(
        api.DELETE('/api/portfolio/projects/{id}/milestones/{milestoneId}', {
          params: { path: { id: projectId, milestoneId }, query: { version } },
        }),
      ),
    onSettled: refresh,
  });
}

export function useAddStandardMilestones(projectId: string) {
  const api = useApi();
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: () =>
      unwrap<MilestoneDetails[]>(
        api.POST('/api/portfolio/projects/{id}/milestones/standard', { params: { path: { id: projectId } } }),
      ),
    onSettled: refresh,
  });
}

/** The next `count` incomplete milestones, soonest first (undated last). */
export function useNextMilestones(projectId: string, count = 5) {
  const query = useMilestones(projectId);
  const items = (query.data?.items ?? [])
    .filter((m) => m.status !== 'Complete')
    .sort((a, b) => {
      const da = dueDate(a);
      const db = dueDate(b);
      if (da === db) return a.sortOrder - b.sortOrder;
      if (da === null) return 1;
      if (db === null) return -1;
      return da < db ? -1 : 1;
    })
    .slice(0, count);
  return { ...query, items };
}

export function useSlippingMilestoneCount(projectId: string): number {
  const { data } = useMilestones(projectId);
  return (data?.items ?? []).filter((m) => isSlipping(m.status)).length;
}
