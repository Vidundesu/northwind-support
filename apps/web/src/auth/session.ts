import type { AuthUser } from '../types/api';

/** What we keep about a signed-in agent. The token is the only credential. */
export interface Session {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthUser;
}

/**
 * The session lives in sessionStorage: it survives a page refresh but not closing the tab,
 * and it is never shared between tabs. The trade-off (any script on the page could read it,
 * i.e. XSS) is written up in the README.
 */
const storageKey = 'northwind.session';

const listeners = new Set<() => void>();

let current: Session | null = readStored();

function isSession(value: unknown): value is Session {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<Session>;

  return (
    typeof candidate.accessToken === 'string' &&
    typeof candidate.expiresAtUtc === 'string' &&
    typeof candidate.user?.id === 'number' &&
    typeof candidate.user.fullName === 'string' &&
    typeof candidate.user.email === 'string'
  );
}

function isExpired(session: Session): boolean {
  return Date.parse(session.expiresAtUtc) <= Date.now();
}

function readStored(): Session | null {
  try {
    const raw = sessionStorage.getItem(storageKey);
    const parsed: unknown = raw ? JSON.parse(raw) : null;

    return isSession(parsed) && !isExpired(parsed) ? parsed : null;
  } catch {
    // Storage blocked or the value is corrupt: behave as signed out.
    return null;
  }
}

function notify() {
  listeners.forEach((listener) => listener());
}

export function getSession(): Session | null {
  return current;
}

export function getAccessToken(): string | null {
  return current?.accessToken ?? null;
}

export function saveSession(session: Session): void {
  current = session;

  try {
    sessionStorage.setItem(storageKey, JSON.stringify(session));
  } catch {
    // Not persisted, but the in-memory session still works for this page load.
  }

  notify();
}

export function clearSession(): void {
  if (current === null) {
    return;
  }

  current = null;

  try {
    sessionStorage.removeItem(storageKey);
  } catch {
    // Nothing more to do.
  }

  notify();
}

/** For useSyncExternalStore. Returns the unsubscribe function. */
export function subscribeToSession(listener: () => void): () => void {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
}
