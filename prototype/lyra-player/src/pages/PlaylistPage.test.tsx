import { describe, expect, it } from 'vitest';

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { loginSession, renderApp } from '../test/render';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend, seedTracks } from '../test/fakeBackend';

describe('PlaylistPage', () => {
  it('turns a prompt into a playable playlist from the local library', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);
    backend.seedAiConnection();

    renderApp({ route: '/playlists', backend, session });

    expect(await screen.findByRole('heading', { name: 'Make a playlist' })).toBeInTheDocument();

    await userEvent.type(
      screen.getByRole('textbox', { name: 'What do you want to hear?' }),
      'Late night drive',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Build playlist' }));

    expect(
      await screen.findByRole('heading', { name: 'Late night drive' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Bohemian Rhapsody')).toBeInTheDocument();
    expect(screen.getByText('Sultans of Swing')).toBeInTheDocument();
    expect(screen.getAllByText(/Fits “Late night drive”/)).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'Play' })).toBeEnabled();
    expect(backend.playlistRequests).toEqual([{ prompt: 'Late night drive', limit: 20 }]);
  });

  it('offers prompt ideas that fill the request field', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/playlists', backend, session });

    await userEvent.click(await screen.findByRole('button', { name: 'Late-night drive' }));

    expect(
      screen.getByRole('textbox', { name: 'What do you want to hear?' }),
    ).toHaveValue('Late-night drive');
  });

  it('shows the backend problem detail when no provider is connected', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/playlists', backend, session });

    await userEvent.type(
      await screen.findByRole('textbox', { name: 'What do you want to hear?' }),
      'Quiet morning',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Build playlist' }));

    expect(
      await screen.findByText(/Connect an AI provider in settings/),
    ).toBeInTheDocument();
  });

  it('shows the backend problem detail when the library has no indexed tracks', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);
    backend.seedAiConnection();

    renderApp({ route: '/playlists', backend, session });

    await userEvent.type(
      await screen.findByRole('textbox', { name: 'What do you want to hear?' }),
      'Quiet morning',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Build playlist' }));

    expect(await screen.findByText(/No indexed tracks/)).toBeInTheDocument();
  });
});
