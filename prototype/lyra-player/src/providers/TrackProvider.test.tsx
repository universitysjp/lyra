import { describe, expect, it } from 'vitest';

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import Player from '../components/Player/Player';
import useTrack from '../hooks/useTrack';
import { renderWithProviders } from '../test/render';

import type { ITrack } from '../types/types';

const track = (overrides: Partial<ITrack> = {}): ITrack => ({
  id: 'track-1',
  localTrackId: 'bohemian-1-0',
  no: 1,
  name: 'Bohemian Rhapsody',
  artist: 'Queen',
  album: 'A Night at the Opera',
  duration: '05:55',
  durationSeconds: 355,
  isEnriched: false,
  hasLyrics: false,
  hasSyncedLyrics: false,
  importedAt: '2026-11-17T10:00:00Z',
  mediaurl: '',
  artwork: 'data:image/svg+xml,test',
  ...overrides,
});

/** Surfaces playback state as text so the provider can be asserted without audio. */
const Probe: React.FC<{ items: ITrack[] }> = ({ items }) => {
  const { currentTrack, missingMedia, prevTrack, nextTrack, queue, playTrack, clearMissingMedia } =
    useTrack();

  return (
    <div>
      <span data-testid='current'>{currentTrack?.name ?? 'none'}</span>
      <span data-testid='missing'>{missingMedia?.name ?? 'none'}</span>
      <span data-testid='neighbours'>{`${prevTrack?.name ?? '-'}|${nextTrack?.name ?? '-'}`}</span>
      <span data-testid='queue'>{String(queue?.length ?? 0)}</span>
      <button type='button' onClick={() => playTrack(items[0], items)}>
        first
      </button>
      <button type='button' onClick={() => playTrack(items[1], items)}>
        second
      </button>
      <button type='button' onClick={() => playTrack(track(), [])}>
        gap
      </button>
      <button type='button' onClick={clearMissingMedia}>
        dismiss
      </button>
    </div>
  );
};

describe('TrackProvider', () => {
  const items = [
    track({ mediaurl: 'blob:first' }),
    track({ id: 'track-2', no: 2, name: 'Sultans of Swing', mediaurl: 'blob:second' }),
    track({ id: 'track-3', no: 3, name: 'Take Five', mediaurl: 'blob:third' }),
  ];

  it('selects a playable track and its queue', async () => {
    renderWithProviders(<Probe items={items} />);

    await userEvent.click(screen.getByRole('button', { name: 'second' }));

    expect(screen.getByTestId('current')).toHaveTextContent('Sultans of Swing');
    expect(screen.getByTestId('queue')).toHaveTextContent('3');
    expect(screen.getByTestId('neighbours')).toHaveTextContent('Bohemian Rhapsody|Take Five');
    expect(screen.getByTestId('missing')).toHaveTextContent('none');
  });

  it('reports metadata-only tracks instead of failing to play', async () => {
    renderWithProviders(<Probe items={items} />);

    await userEvent.click(screen.getByRole('button', { name: 'gap' }));

    expect(screen.getByTestId('missing')).toHaveTextContent('Bohemian Rhapsody');
    expect(screen.getByTestId('current')).toHaveTextContent('none');

    await userEvent.click(screen.getByRole('button', { name: 'dismiss' }));

    expect(screen.getByTestId('missing')).toHaveTextContent('none');
  });

  it('keeps queue neighbours correct at the edges', async () => {
    renderWithProviders(<Probe items={items} />);

    await userEvent.click(screen.getByRole('button', { name: 'first' }));

    expect(screen.getByTestId('neighbours')).toHaveTextContent('-|Sultans of Swing');
  });

  it('renders the player bar only when a track is selected', async () => {
    renderWithProviders(
      <>
        <Player />
        <Probe items={items} />
      </>,
    );

    expect(document.querySelector('.player')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'first' }));

    expect(document.querySelector('.player')).not.toBeNull();
    expect(screen.getByTestId('current')).toHaveTextContent('Bohemian Rhapsody');
  });
});
