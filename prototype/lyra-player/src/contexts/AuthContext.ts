import { createContext } from 'react';

import type { Session } from '../api/session';

export type AuthStatus = 'loading' | 'anonymous' | 'authenticated';

export interface IAuthContext {
  status: AuthStatus;
  session: Session | null;
  error: string | null;
  busy: boolean;
  /** Epoch ms when the current access token expires, 0 when signed out. */
  expiresAt: number;
  login: (email: string, password: string) => Promise<boolean>;
  register: (email: string, password: string) => Promise<boolean>;
  logout: () => Promise<void>;
  dismissError: () => void;
}

const AuthContext = createContext<IAuthContext>({
  status: 'loading',
  session: null,
  error: null,
  busy: false,
  expiresAt: 0,
  login: async () => false,
  register: async () => false,
  logout: async () => undefined,
  dismissError: () => undefined,
});

export default AuthContext;
