import { describe, expect, it } from 'vitest';

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { loginSession, renderApp } from '../test/render';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend, seedTracks } from '../test/fakeBackend';

describe('ImportPage', () => {
  it('syncs the demo batch and reports what the backend accepted', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/import', backend, session });

    await screen.findByRole('heading', { name: 'Sync local track metadata' });

    await userEvent.click(screen.getByRole('button', { name: /Load demo batch \(6 tracks\)/ }));

    expect(await screen.findByText('last import: 6 new / 0 duplicates')).toBeInTheDocument();
    expect(backend.batchCalls).toHaveLength(1);
    expect(backend.batchCalls[0]).toHaveLength(6);
  });

  it('shows fingerprint dedupe when the same batch is sent twice', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/import', backend, session });

    await screen.findByRole('heading', { name: 'Sync local track metadata' });

    const button = screen.getByRole('button', { name: /Load demo batch/ });

    await userEvent.click(button);
    await screen.findByText('last import: 6 new / 0 duplicates');

    await userEvent.click(screen.getByRole('button', { name: /Load demo batch/ }));

    expect(await screen.findByText('last import: 0 new / 6 duplicates')).toBeInTheDocument();
    expect(backend.batchCalls).toHaveLength(2);

    await userEvent.click(screen.getByRole('link', { name: 'Back to library' }));
    expect(await screen.findByRole('heading', { name: 'Your library' })).toBeInTheDocument();
    expect(await screen.findByText('Bohemian Rhapsody')).toBeInTheDocument();
  });

  it('explains the batch endpoint and the local-only audio rule', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/import', backend, session });

    expect(
      await screen.findByText(/Drop local audio files here\. Tags are read in your browser/),
    ).toBeInTheDocument();
    expect(screen.getByText(/no import yet/)).toBeInTheDocument();
  });
});
