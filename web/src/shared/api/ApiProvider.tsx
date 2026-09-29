import { useMemo, type ReactNode } from 'react';
import { useSession } from '../auth/session';
import { useConfig } from '../config';
import { ApiContext, createApi } from './client';

export function ApiProvider({ children }: { children: ReactNode }) {
  const { apiBaseUrl } = useConfig();
  const session = useSession();
  const api = useMemo(
    () => createApi(apiBaseUrl, { getToken: session.getToken, onUnauthorized: session.signIn }),
    [apiBaseUrl, session],
  );
  return <ApiContext.Provider value={api}>{children}</ApiContext.Provider>;
}
