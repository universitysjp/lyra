import { describe, expect, it } from 'vitest';

import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { renderApp, loginSession } from '../test/render';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend, seedTracks } from '../test/fakeBackend';

describe('LibraryPage', () => {
  it('lists the synced tracks with totals and pager', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/library', backend, session });

    expect(await screen.findByRole('heading', { name: 'Your library' })).toBeInTheDocument();
    expect(await screen.findByText('Bohemian Rhapsody')).toBeInTheDocument();
    expect(screen.getByText('Sultans of Swing')).toBeInTheDocument();
    expect(screen.getByText('2 tracks · 12 min on this page')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Link file' })).toHaveLength(2);
    expect(screen.getByText('1–2 of 2')).toBeInTheDocument();
    expect(screen.getByText('Page 1 of 1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
  });

  it('explains the local-first model when nothing is synced yet', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/library', backend, session });

    expect(await screen.findByText('Your library is quiet')).toBeInTheDocument();
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  it('keeps the signed-in account visible in the top bar', async () => {
    const backend = createFakeBackend({ tracks: seedTracks });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/library', backend, session });

    await screen.findByText('Bohemian Rhapsody');

    const sidebar = document.querySelector('.sidebar') as HTMLElement;

    expect(within(sidebar).getByText(DEMO_EMAIL)).toBeInTheDocument();
    expect(within(sidebar).getByRole('button', { name: 'Sign out' })).toBeInTheDocument();
    expect(screen.queryByText(/^token /)).not.toBeInTheDocument();
  });

  it('routes an anonymous visitor to the login page', async () => {
    const backend = createFakeBackend();

    renderApp({ route: '/library', backend, session: null });

    expect(await screen.findByRole('heading', { name: /Your music/ })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('walks the backend pages with the pager', async () => {
    const backend = createFakeBackend({
      tracks: Array.from({ length: 55 }, (_, index) => ({
        localTrackId: `local-${index}`,
        title: `Track ${index + 1}`,
        artist: 'Long Library',
        album: 'Volume 1',
        durationSeconds: 200,
      })),
    });
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/library', backend, session });

    expect(await screen.findByText('Track 55')).toBeInTheDocument();
    expect(screen.getByText('1–50 of 55')).toBeInTheDocument();
    expect(screen.getByText('Page 1 of 2')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));

    expect(await screen.findByText('51–55 of 55')).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument();
    expect(screen.queryByText('Track 50')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled();

    await userEvent.click(screen.getByRole('button', { name: 'Previous' }));

    expect(await screen.findByText('1–50 of 55')).toBeInTheDocument();
  });
});
