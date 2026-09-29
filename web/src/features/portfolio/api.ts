import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type { paths } from '../../shared/api/schema';
import type { ListQuery, ProjectDetails, ProjectInput, ProjectPage, ProjectUpdateInput } from './types';

type WireQuery = NonNullable<paths['/api/portfolio/projects']['get']['parameters']['query']>;

function toWireQuery(query: ListQuery): WireQuery {
  return {
    Stage: query.stage,
    Status: query.status,
    Search: query.search,
    IncludeArchived: query.includeArchived,
    Page: query.page,
    PageSize: query.pageSize,
  };
}

function useStoreProject() {
  const queryClient = useQueryClient();
  return (project: ProjectDetails) => {
    queryClient.setQueryData(['project', project.id], project);
    void queryClient.invalidateQueries({ queryKey: ['projects'] });
  };
}

export function useProjects(query: ListQuery) {
  const api = useApi();
  return useQuery({
    queryKey: ['projects', query],
    queryFn: () => unwrap<ProjectPage>(api.GET('/api/portfolio/projects', { params: { query: toWireQuery(query) } })),
    placeholderData: keepPreviousData,
  });
}

export function useProject(id: string) {
  const api = useApi();
  return useQuery({
    queryKey: ['project', id],
    queryFn: () => unwrap<ProjectDetails>(api.GET('/api/portfolio/projects/{id}', { params: { path: { id } } })),
  });
}

export function useCreateProject() {
  const api = useApi();
  const store = useStoreProject();
  return useMutation({
    mutationFn: (body: ProjectInput) => unwrap<ProjectDetails>(api.POST('/api/portfolio/projects', { body })),
    onSuccess: store,
  });
}

export function useUpdateProject(id: string) {
  const api = useApi();
  const store = useStoreProject();
  return useMutation({
    mutationFn: (body: ProjectUpdateInput) =>
      unwrap<ProjectDetails>(api.PUT('/api/portfolio/projects/{id}', { params: { path: { id } }, body })),
    onSuccess: store,
  });
}

export function useSetArchived(id: string) {
  const api = useApi();
  const store = useStoreProject();
  return useMutation({
    mutationFn: ({ archived, version }: { archived: boolean; version: number }) =>
      unwrap<ProjectDetails>(
        archived
          ? api.POST('/api/portfolio/projects/{id}/archive', { params: { path: { id } }, body: { version } })
          : api.POST('/api/portfolio/projects/{id}/restore', { params: { path: { id } }, body: { version } }),
      ),
    onSuccess: store,
  });
}
