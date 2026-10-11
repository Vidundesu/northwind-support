import { createContext } from 'react';
import type { AuthUser, LoginCredentials } from '../types/api';

export interface AuthContextValue {
  user: AuthUser | null;
  isAuthenticated: boolean;
  /** Rejects with an ApiError when the credentials are wrong. */
  login: (credentials: LoginCredentials) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
