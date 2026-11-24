import { describe, expect, it } from 'vite-plus/test';

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { loginSession, renderApp } from '../test/render';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend, seedTracks } from '../test/fakeBackend';

describe('ImportPage', () => {
  it('presents the add-music flow without demo content', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/import', backend, session });

    expect(await screen.findByRole('heading', { name: 'Add music' })).toBeInTheDocument();
    expect(screen.getByText('Drop your music here')).toBeInTheDocument();
    expect(screen.getByText('Your files stay here')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /demo batch/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/POST \/api\/catalog/)).not.toBeInTheDocument();
  });

  it('links back to the library', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/import', backend, session });

    await userEvent.click(await screen.findByRole('link', { name: 'Back to library' }));

    expect(await screen.findByRole('heading', { name: 'Your library' })).toBeInTheDocument();
  });
});
