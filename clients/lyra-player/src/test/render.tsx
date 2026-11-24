import type { ReactNode } from 'react';

import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

import { createFakeBackend } from './fakeBackend';
import ApiProvider from '../providers/ApiProvider';
import AuthProvider from '../providers/AuthProvider';
import LibraryProvider from '../providers/LibraryProvider';
import TrackProvider from '../providers/TrackProvider';
import Navigation from '../navigation/Navigation';

import type { FakeBackend } from './fakeBackend';
import type { Session } from '../api/session';

export interface RenderOptions {
  route?: string;
  backend?: FakeBackend;
  session?: Session | null;
}

/** Signs in through the fake gateway and returns a session the providers can be seeded with. */
export const loginSession = async (
  backend: FakeBackend,
  email: string,
  password: string,
): Promise<Session> => {
  const response = (await backend.fetchImpl('http://localhost/api/auth/login', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })) as Response;

  const issued = (await response.json()) as {
    accessToken: string;
    refreshToken: string;
    expiresInSeconds: number;
    userId: string;
  };

  return {
    userId: issued.userId,
    email,
    accessToken: issued.accessToken,
    refreshToken: issued.refreshToken,
    expiresAt: Date.now() + issued.expiresInSeconds * 1000,
  };
};

const withProviders = (
  children: ReactNode,
  backend: FakeBackend,
  session: Session | null,
  route: string,
): ReactNode => (
  <MemoryRouter initialEntries={[route]}>
    <ApiProvider fetchImpl={backend.fetchImpl} session={session}>
      <AuthProvider>
        <LibraryProvider>
          <TrackProvider>{children}</TrackProvider>
        </LibraryProvider>
      </AuthProvider>
    </ApiProvider>
  </MemoryRouter>
);

/** Renders a fragment inside the full provider stack, backed by the fake gateway. */
export const renderWithProviders = (ui: ReactNode, options: RenderOptions = {}) => {
  const backend = options.backend ?? createFakeBackend();
  const session = options.session ?? null;

  return { ...render(withProviders(ui, backend, session, options.route ?? '/')), backend };
};

/** Renders the routed application, like the browser build does. */
export const renderApp = (options: RenderOptions = {}) =>
  renderWithProviders(<Navigation />, { route: '/', ...options });
