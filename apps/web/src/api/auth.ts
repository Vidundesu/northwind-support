import type { LoginCredentials, LoginResponse } from '../types/api';
import { request } from './client';

export const authApi = {
  /** Anonymous by design: a wrong password is a 401 the login form shows, not a session expiry. */
  login: (credentials: LoginCredentials) =>
    request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(credentials),
      anonymous: true,
    }),
};
