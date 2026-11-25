import { beforeEach, describe, expect, it, vi } from 'vite-plus/test';

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import LyricsPanel from './LyricsPanel';
import useTrack from '../../hooks/useTrack';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend } from '../../test/fakeBackend';
import { loginSession, renderWithProviders } from '../../test/render';

import type { ITrack } from '../../types/types';

const track = (overrides: Partial<ITrack> = {}): ITrack => ({
  id: 'track-1',
  localTrackId: 'local-1',
  no: 1,
  name: 'Bohemian Rhapsody',
  artist: 'Queen',
  album: 'A Night at the Opera',
  duration: '05:55',
  durationSeconds: 355,
  isEnriched: true,
  hasLyrics: true,
  hasSyncedLyrics: true,
  importedAt: '2026-11-17T10:00:00Z',
  mediaurl: 'blob:test',
  artwork: 'data:image/svg+xml,test',
  ...overrides,
});

const Probe: React.FC<{ item: ITrack }> = ({ item }) => {
  const { playTrack } = useTrack();

  return (
    <button type='button' onClick={() => playTrack(item, [item])}>
      play
    </button>
  );
};

describe('LyricsPanel', () => {
  let backend: ReturnType<typeof createFakeBackend>;

  beforeEach(() => {
    backend = createFakeBackend({
      tracks: [
        {
          localTrackId: 'local-1',
          title: 'Bohemian Rhapsody',
          artist: 'Queen',
          album: 'A Night at the Opera',
          durationSeconds: 355,
        },
      ],
    });
  });

  const render = async (): Promise<void> => {
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderWithProviders(
      <>
        <Probe item={track()} />
        <LyricsPanel open onClose={vi.fn()} />
      </>,
      { session, backend },
    );

    await userEvent.click(screen.getByRole('button', { name: 'play' }));
  };

  it('renders synced lyrics when the track has timestamps', async () => {
    backend.setLyrics('track-1', {
      plainLyrics: 'plain version',
      syncedLyrics: '[00:01.00]first synced line\n[00:05.00]second synced line',
    });

    await render();

    expect(await screen.findByText('first synced line')).toBeInTheDocument();
    expect(screen.getByText('second synced line')).toBeInTheDocument();
    expect(screen.queryByText('plain version')).not.toBeInTheDocument();
    expect(screen.getByText('Tap a line to jump to it.')).toBeInTheDocument();
  });

  it('falls back to plain lyrics when no synced version exists', async () => {
    backend.setLyrics('track-1', {
      plainLyrics: 'plain line one\nplain line two',
      syncedLyrics: null,
    });

    await render();

    expect(await screen.findByText('plain line one')).toBeInTheDocument();
    expect(screen.getByText('plain line two')).toBeInTheDocument();
    expect(screen.queryByText('Tap a line to jump to it.')).not.toBeInTheDocument();
  });
});
