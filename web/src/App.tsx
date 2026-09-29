import { createTheme, DEFAULT_THEME, MantineProvider } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { routes } from './routes';
import { ApiError } from './shared/api/client';
import { ApiProvider } from './shared/api/ApiProvider';
import { SessionProvider } from './shared/auth/session';
import { ConfigContext, type AppConfig } from './shared/config';

export function App({ config }: { config: AppConfig }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
          },
        },
      }),
  );
  const [router] = useState(() => createBrowserRouter(routes));
  const primaryColor = config.primaryColor in DEFAULT_THEME.colors ? config.primaryColor : 'teal';

  return (
    <ConfigContext.Provider value={config}>
      <MantineProvider theme={createTheme({ primaryColor })}>
        <Notifications />
        <QueryClientProvider client={queryClient}>
          <SessionProvider config={config}>
            <ApiProvider>
              <RouterProvider router={router} />
            </ApiProvider>
          </SessionProvider>
        </QueryClientProvider>
      </MantineProvider>
    </ConfigContext.Provider>
  );
}
