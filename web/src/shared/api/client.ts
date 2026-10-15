import createClient, { type Client, type Middleware } from 'openapi-fetch';
import { createContext, useContext } from 'react';
import type { paths } from './schema';

export type Api = Client<paths>;

export type ProblemDetails = {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
  correlationId?: string;
};

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

export const REQUEST_TIMEOUT_MS = 30_000;

/**
 * fetch with a timeout. The timeout covers time-to-headers only (it is cleared once
 * response headers arrive), not body streaming. Caller-supplied abort signals (on
 * request.signal) still abort the request and surface as the original AbortError.
 */
async function fetchWithTimeout(request: Request): Promise<Response> {
  const controller = new AbortController();
  let timedOut = false;
  const onCallerAbort = () => controller.abort();
  if (request.signal.aborted) controller.abort();
  else request.signal.addEventListener('abort', onCallerAbort, { once: true });
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, REQUEST_TIMEOUT_MS);
  try {
    return await globalThis.fetch(new Request(request, { signal: controller.signal }));
  } catch (error) {
    if (timedOut) {
      throw new ApiError(408, {
        title: 'The server took too long to respond',
        detail: 'Check your connection and try again.',
        status: 408,
      });
    }
    throw error;
  } finally {
    clearTimeout(timer);
    request.signal.removeEventListener('abort', onCallerAbort);
  }
}

export function createApi(
  baseUrl: string,
  options: { getToken: () => string | undefined; onUnauthorized?: () => void },
): Api {
  const client = createClient<paths>({ baseUrl, fetch: fetchWithTimeout });
  const auth: Middleware = {
    onRequest({ request }) {
      const token = options.getToken();
      if (token) request.headers.set('Authorization', `Bearer ${token}`);
      return request;
    },
    onResponse({ response }) {
      if (response.status === 401) options.onUnauthorized?.();
      return response;
    },
  };
  client.use(auth);
  return client;
}

/** Returns the response body, or throws ApiError carrying the problem details. */
export async function unwrap<T>(request: Promise<{ data?: unknown; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await request;
  if (!response.ok) throw new ApiError(response.status, (error ?? undefined) as ProblemDetails | undefined);
  return data as T;
}

export const ApiContext = createContext<Api | null>(null);

export function useApi(): Api {
  const api = useContext(ApiContext);
  if (!api) throw new Error('useApi must be used inside ApiContext');
  return api;
}
