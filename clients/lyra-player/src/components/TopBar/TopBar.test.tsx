import { describe, expect, it } from 'vite-plus/test';

import { screen, within } from '@testing-library/react';
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
    expect(await screen.findByRole('heading', { name: 'Albums' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('link', { name: 'Search' }));
    expect(await screen.findByRole('heading', { name: 'Search' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('link', { name: 'Add music' }));
    expect(await screen.findByRole('heading', { name: 'Add music' })).toBeInTheDocument();
  });

  it('keeps the signed-in account in the sidebar without exposing token state', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/library', backend, session });

    await screen.findByRole('heading', { name: 'Your library' });

    const sidebar = document.querySelector('.sidebar') as HTMLElement;

    expect(within(sidebar).getByText(DEMO_EMAIL)).toBeInTheDocument();
    expect(within(sidebar).getByRole('button', { name: 'Sign out' })).toBeInTheDocument();
    expect(screen.queryByText(/^token /)).not.toBeInTheDocument();
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
