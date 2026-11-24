import { describe, expect, it } from 'vitest';

import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { loginSession, renderApp } from '../test/render';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend, seedTracks } from '../test/fakeBackend';

describe('AlbumsPage', () => {
  it('groups the library into album cards', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/albums', backend, session });

    expect(await screen.findByRole('heading', { name: 'Albums in your library' })).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getAllByRole('link').filter((link) =>
        link.getAttribute('href')?.startsWith('/album/'),
      )).toHaveLength(2),
    );

    const cards = screen.getAllByRole('link').filter((link) => link.getAttribute('href')?.startsWith('/album/'));
    const queenCard = cards.find(
      (card) => card.getAttribute('href') === '/album/queen%2Fa-night-at-the-opera',
    );

    expect(queenCard).toBeDefined();
    expect(within(queenCard as HTMLElement).getByText('A Night at the Opera')).toBeInTheDocument();
    expect(cards.map((card) => card.getAttribute('href'))).toContain(
      '/album/dire-straits%2Fdire-straits',
    );
  });

  it('opens an album and plays one of its tracks', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/albums', backend, session });

    const card = await screen.findByRole('link', { name: /A Night at the Opera/ });

    await userEvent.click(card);

    expect(await screen.findByRole('heading', { level: 2, name: 'A Night at the Opera' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Queen' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Queen' })).toHaveAttribute('href', '/albums');
    expect(screen.getByText('1 Songs - 6 Minutes')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Play Bohemian Rhapsody' })).toBeInTheDocument();
  });

  it('warns when the album has no local audio', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/album/queen%2Fa-night-at-the-opera', backend, session });

    await screen.findByRole('heading', { level: 2, name: 'A Night at the Opera' });

    const row = screen.getByText('Bohemian Rhapsody').closest('.track') as HTMLElement;

    expect(within(row).getByText('metadata only')).toBeInTheDocument();
    expect(within(row).getByRole('button', { name: 'Attach file' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Play or pause this album' }));

    expect(await screen.findByText(/has no audio file on this device yet/)).toBeInTheDocument();
  });

  it('offers a way back when the album id is unknown', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/album/nobody%2Fno-album', backend, session });

    expect(await screen.findByText('Album not found in this library page.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to albums' })).toHaveAttribute('href', '/albums');
  });
});
