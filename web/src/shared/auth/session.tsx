import { createContext, useContext, useEffect, useMemo, useRef, type ReactNode } from 'react';
import { AuthProvider, useAuth } from 'react-oidc-context';
import type { AppConfig } from '../config';
import { FullPageMessage } from '../layout/FullPageMessage';

export type Session = {
  getToken: () => string | undefined;
  signIn: () => void;
  signOut: () => void;
};

export const SessionContext = createContext<Session | null>(null);

export function useSession(): Session {
  const session = useContext(SessionContext);
  if (!session) throw new Error('useSession must be used inside SessionProvider');
  return session;
}

/** Local dev: the API signs every request in as the configured dev user, so no token is needed. */
const developmentSession: Session = {
  getToken: () => undefined,
  signIn: () => window.location.reload(),
  signOut: () => window.location.assign('/'),
};

export function SessionProvider({ config, children }: { config: AppConfig; children: ReactNode }) {
  if (config.auth.mode === 'Development') {
    return <SessionContext.Provider value={developmentSession}>{children}</SessionContext.Provider>;
  }

  const origin = window.location.origin;
  return (
    <AuthProvider
      authority={config.auth.authority ?? ''}
      client_id={config.auth.clientId ?? ''}
      redirect_uri={`${origin}/`}
      scope="openid email profile"
      onSigninCallback={() => window.history.replaceState({}, document.title, window.location.pathname)}
    >
      <CognitoSession config={config}>{children}</CognitoSession>
    </AuthProvider>
  );
}

function CognitoSession({ config, children }: { config: AppConfig; children: ReactNode }) {
  const auth = useAuth();
  const token = useRef<string | undefined>(undefined);
  token.current = auth.user?.id_token;

  useEffect(() => {
    if (!auth.isLoading && !auth.isAuthenticated && !auth.activeNavigator && !auth.error) {
      void auth.signinRedirect();
    }
  }, [auth]);

  const session = useMemo<Session>(
    () => ({
      getToken: () => token.current,
      signIn: () => void auth.signinRedirect(),
      signOut: () => {
        void auth.removeUser();
        const logout = new URL('/logout', config.auth.logoutDomain ?? window.location.origin);
        logout.searchParams.set('client_id', config.auth.clientId ?? '');
        logout.searchParams.set('logout_uri', `${window.location.origin}/`);
        window.location.assign(logout.toString());
      },
    }),
    [auth, config.auth.clientId, config.auth.logoutDomain],
  );

  if (auth.error) return <FullPageMessage title="Sign-in failed" message={auth.error.message} />;
  if (!auth.isAuthenticated) return <FullPageMessage title="Signing you in…" loading />;
  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}
