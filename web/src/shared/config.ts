import { createContext, useContext } from 'react';
import type { paths } from './api/schema';
import type { DeepRequired } from './api/types';

export type ServerConfig = DeepRequired<paths['/api/config']['get']['responses'][200]['content']['application/json']>;
export type AppConfig = ServerConfig & { apiBaseUrl: string };

async function getJson<T>(fetchImpl: typeof fetch, url: string): Promise<T> {
  const response = await fetchImpl(url, { headers: { Accept: 'application/json' } });
  if (!response.ok) throw new Error(`GET ${url} failed with status ${response.status}`);
  return (await response.json()) as T;
}

/** Reads /config.json (API address for this deployment), then the server's branding, locale and auth settings. */
export async function loadConfig(fetchImpl: typeof fetch = fetch): Promise<AppConfig> {
  const bootstrap = await getJson<{ apiBaseUrl: string }>(fetchImpl, '/config.json');
  const apiBaseUrl = bootstrap.apiBaseUrl.replace(/\/+$/, '');
  const server = await getJson<ServerConfig>(fetchImpl, `${apiBaseUrl}/api/config`);
  return { ...server, apiBaseUrl };
}

export const ConfigContext = createContext<AppConfig | null>(null);

export function useConfig(): AppConfig {
  const config = useContext(ConfigContext);
  if (!config) throw new Error('useConfig must be used inside ConfigContext');
  return config;
}
