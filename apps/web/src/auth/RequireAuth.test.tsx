import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthProvider } from './AuthProvider';
import { RequireAuth } from './RequireAuth';
import { clearSession, saveSession } from './session';

function renderAt(path: string) {
  const router = createMemoryRouter(
    [
      { path: '/login', element: <p>Login screen</p> },
      { element: <RequireAuth />, children: [{ path: '/tickets/:id', element: <p>Ticket page</p> }] },
    ],
    { initialEntries: [path] },
  );

  render(
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>,
  );

  return router;
}

describe('RequireAuth', () => {
  beforeEach(() => clearSession());

  it('redirects an unauthenticated visitor to /login and remembers the page they wanted', () => {
    const router = renderAt('/tickets/7?tab=history');

    expect(screen.getByText('Login screen')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toMatchObject({ from: { pathname: '/tickets/7', search: '?tab=history' } });
  });

  it('shows the page to a signed-in agent', () => {
    saveSession({
      accessToken: 't',
      expiresAtUtc: '2999-01-01T00:00:00Z',
      user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind.test' },
    });

    renderAt('/tickets/7');

    expect(screen.getByText('Ticket page')).toBeInTheDocument();
  });
});
