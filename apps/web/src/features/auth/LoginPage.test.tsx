import { fireEvent, render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider } from '../../auth/AuthProvider';
import { RequireAuth } from '../../auth/RequireAuth';
import { clearSession, getSession } from '../../auth/session';
import { LoginPage } from './LoginPage';

function renderFrom(path: string) {
  const router = createMemoryRouter(
    [
      { path: '/login', element: <LoginPage /> },
      {
        element: <RequireAuth />,
        children: [
          { path: '/tickets', element: <p>Ticket list</p> },
          { path: '/tickets/:id', element: <p>Ticket detail</p> },
        ],
      },
    ],
    { initialEntries: [path] },
  );

  render(
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>,
  );
}

function fillAndSubmit(email: string, password: string) {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('LoginPage', () => {
  beforeEach(() => clearSession());
  afterEach(() => vi.unstubAllGlobals());

  it('blocks empty or malformed input before calling the API', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    renderFrom('/login');

    fillAndSubmit('not-an-email', '');

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('shows the generic error for rejected credentials and stores no session', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ title: 'Invalid email or password.' }), { status: 401 })),
    );
    renderFrom('/login');

    fillAndSubmit('alex.turner@northwind.test', 'wrong');

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(getSession()).toBeNull();
  });

  it('signs in and returns the visitor to the page they were trying to reach', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            accessToken: 'jwt',
            expiresAtUtc: '2999-01-01T00:00:00Z',
            user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind.test' },
          }),
          { status: 200 },
        ),
      ),
    );
    renderFrom('/tickets/7'); // signed out -> bounced to /login with the target remembered

    fillAndSubmit('alex.turner@northwind.test', 'DemoOnly!ChangeMe2026');

    expect(await screen.findByText('Ticket detail')).toBeInTheDocument();
    expect(getSession()?.user.fullName).toBe('Alex Turner');
  });
});
