import { describe, expect, it } from 'vitest';

import { screen } from '@testing-library/react';

import { loginSession, renderApp } from '../test/render';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend, seedTracks } from '../test/fakeBackend';

describe('SearchPage', () => {
  it('returns fuzzy matches for a misspelled title', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/search?q=bohemain%20rhapsody', backend, session });

    expect(await screen.findByText(/Closest matches for "bohemain rhapsody"/)).toBeInTheDocument();
    expect(await screen.findByText('Bohemian Rhapsody')).toBeInTheDocument();
    expect(screen.queryByText('Sultans of Swing')).not.toBeInTheDocument();
    expect(backend.requests.some((entry) => entry.startsWith('GET /api/catalog/tracks/search'))).toBe(
      true,
    );
  });

  it('says so when nothing is close enough', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/search?q=zzzzzz', backend, session });

    expect(await screen.findByText('No close matches in your library.')).toBeInTheDocument();
  });

  it('prompts for a term when the query is empty and calls nothing', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/search', backend, session });

    expect(
      await screen.findByText(/Type something above to search the tracks you synced/),
    ).toBeInTheDocument();
    expect(
      backend.requests.filter((entry) => entry.startsWith('GET /api/catalog/tracks/search')),
    ).toHaveLength(0);
  });

  it('matches by artist as well', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/search?q=straits', backend, session });

    expect(await screen.findByText('Sultans of Swing')).toBeInTheDocument();
  });
});
