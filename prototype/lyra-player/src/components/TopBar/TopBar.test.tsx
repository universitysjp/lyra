import { describe, expect, it } from 'vitest';

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { loginSession, renderApp } from '../../test/render';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend, seedTracks } from '../../test/fakeBackend';

describe('TopBar', () => {
  it('navigates between the sections of the player', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/library', backend, session });

    await screen.findByRole('heading', { name: 'Your library' });

    await userEvent.click(screen.getByRole('link', { name: 'Albums' }));
    expect(await screen.findByRole('heading', { name: 'Albums in your library' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('link', { name: 'Search' }));
    expect(await screen.findByText(/Search your library/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('link', { name: 'Sync metadata' }));
    expect(await screen.findByRole('heading', { name: 'Sync local track metadata' })).toBeInTheDocument();
  });

  it('shows the remaining access token lifetime', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/library', backend, session });

    await screen.findByRole('heading', { name: 'Your library' });

    const chip = screen.getByText(/^token /);

    expect(chip).toHaveTextContent(/token (0[0-9]|1[0-5]):[0-5][0-9]/);
  });

  it('signs out and revokes the refresh token', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/library', backend, session });

    await screen.findByRole('heading', { name: 'Your library' });

    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('tab', { name: 'Sign in' })).toBeInTheDocument();
    expect(backend.requests).toContain('POST /api/auth/logout');
  });
});
