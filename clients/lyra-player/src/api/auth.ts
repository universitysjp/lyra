import type { LyraClient } from './http';
import type { Session } from './session';
import type { AuthResponse, MeResponse } from '../types/lyra';

export interface AuthApi {
  login(email: string, password: string): Promise<Session>;
  register(email: string, password: string): Promise<Session>;
  me(): Promise<MeResponse>;
  logout(): Promise<void>;
}

const toSession = (issued: AuthResponse, email: string): Session => ({
  userId: issued.userId,
  email,
  accessToken: issued.accessToken,
  refreshToken: issued.refreshToken,
  expiresAt: Date.now() + issued.expiresInSeconds * 1000,
});

/** Lyra.Identity.Api endpoints: POST /api/auth/register, /login, /logout and GET /me. */
export const createAuthApi = (client: LyraClient): AuthApi => ({
  async login(email, password) {
    const issued = await client.request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      auth: false,
      skipPreRefresh: true,
      body: { email, password },
    });

    const session = toSession(issued, email);
    client.saveSession(session);

    return session;
  },

  async register(email, password) {
    const issued = await client.request<AuthResponse>('/api/auth/register', {
      method: 'POST',
      auth: false,
      skipPreRefresh: true,
      body: { email, password },
    });

    const session = toSession(issued, email);
    client.saveSession(session);

    return session;
  },

  me() {
    return client.request<MeResponse>('/api/auth/me');
  },

  async logout() {
    const refreshToken = client.session()?.refreshToken;

    try {
      await client.request<void>('/api/auth/logout', {
        method: 'POST',
        auth: false,
        skipPreRefresh: true,
        body: { refreshToken: refreshToken ?? '' },
      });
    } finally {
      client.clearSession();
    }
  },
});
