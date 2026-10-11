import { useCallback, useEffect, useMemo, useSyncExternalStore } from 'react';
import { authApi } from '../api/auth';
import type { LoginCredentials } from '../types/api';
import { AuthContext, type AuthContextValue } from './AuthContext';
import { clearSession, getSession, saveSession, subscribeToSession } from './session';

/** Largest delay setTimeout accepts; longer ones fire immediately. */
const maxTimerDelayMs = 2_147_483_647;

/** Exposes the signed-in agent to the tree and ends the session when the token expires. */
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const session = useSyncExternalStore(subscribeToSession, getSession);
  const expiresAtUtc = session?.expiresAtUtc;

  const login = useCallback(async (credentials: LoginCredentials) => {
    const response = await authApi.login(credentials);

    saveSession({
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      user: response.user,
    });
  }, []);

  // Sign out when the token runs out, rather than waiting for the next call to fail with a 401.
  useEffect(() => {
    if (!expiresAtUtc) {
      return undefined;
    }

    const remaining = Date.parse(expiresAtUtc) - Date.now();
    const timer = window.setTimeout(clearSession, Math.min(Math.max(remaining, 0), maxTimerDelayMs));

    return () => window.clearTimeout(timer);
  }, [expiresAtUtc]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ?? null,
      isAuthenticated: session !== null,
      login,
      logout: clearSession,
    }),
    [session, login],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}
