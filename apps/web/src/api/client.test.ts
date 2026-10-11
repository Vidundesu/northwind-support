import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearSession, getSession, saveSession } from '../auth/session';
import { request } from './client';

const session = {
  accessToken: 'token-123',
  expiresAtUtc: '2999-01-01T00:00:00Z',
  user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind.test' },
};

function respond(status: number, body: unknown = {}) {
  return vi.fn().mockResolvedValue(new Response(JSON.stringify(body), { status }));
}

describe('api client authentication', () => {
  beforeEach(() => clearSession());
  afterEach(() => vi.unstubAllGlobals());

  it('attaches the bearer token to authenticated calls', async () => {
    const fetchMock = respond(200);
    vi.stubGlobal('fetch', fetchMock);
    saveSession(session);

    await request('/tickets');

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect((init.headers as Record<string, string>).Authorization).toBe('Bearer token-123');
  });

  it('sends no token when signed out', async () => {
    const fetchMock = respond(200);
    vi.stubGlobal('fetch', fetchMock);

    await request('/tickets');

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(init.headers).not.toHaveProperty('Authorization');
  });

  it('ends the session when an authenticated call returns 401', async () => {
    vi.stubGlobal('fetch', respond(401, { title: 'Unauthorized' }));
    saveSession(session);

    await expect(request('/tickets')).rejects.toMatchObject({ status: 401 });

    expect(getSession()).toBeNull();
  });

  it('keeps the session when an anonymous call (login) returns 401', async () => {
    vi.stubGlobal('fetch', respond(401, { title: 'Invalid email or password.' }));
    saveSession(session);

    await expect(request('/auth/login', { anonymous: true })).rejects.toMatchObject({ status: 401 });

    expect(getSession()).not.toBeNull();
  });
});
