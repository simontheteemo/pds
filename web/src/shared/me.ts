import { useQuery } from '@tanstack/react-query';
import { unwrap, useApi } from './api/client';
import type { paths } from './api/schema';
import type { DeepRequired } from './api/types';

export type Me = DeepRequired<paths['/api/me']['get']['responses'][200]['content']['application/json']>;

export function useMe() {
  const api = useApi();
  return useQuery({
    queryKey: ['me'],
    queryFn: () => unwrap<Me>(api.GET('/api/me')),
    staleTime: 5 * 60_000,
  });
}

/** For showing or hiding actions only; the API enforces every permission. */
export function usePermissions() {
  const roles = useMe().data?.roles ?? [];
  return {
    canWrite: roles.includes('Manager') || roles.includes('Admin'),
    canAdminister: roles.includes('Admin'),
  };
}
