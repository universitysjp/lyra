import { describe, expect, it, vi } from 'vitest';

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import TrackList from './TrackList';
import { renderWithProviders } from '../../test/render';

import type { ITrack } from '../../types/types';

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

describe('TrackList', () => {
  it('shows the empty label when the library page is empty', () => {
    renderWithProviders(<TrackList tracks={[]} onAttach={vi.fn()} emptyLabel='Nothing synced yet.' />);

    expect(screen.getByText('Nothing synced yet.')).toBeInTheDocument();
  });

  it('renders one row per track with its catalog metadata', () => {
    renderWithProviders(<TrackList tracks={[track(), track({ id: 'track-2', name: 'Take Five' })]} onAttach={vi.fn()} />);

    expect(screen.getByRole('list')).toBeInTheDocument();
    expect(screen.getByText('Bohemian Rhapsody')).toBeInTheDocument();
    expect(screen.getAllByText('Queen')).toHaveLength(2);
    expect(screen.getByText('Take Five')).toBeInTheDocument();
    expect(screen.getAllByText('05:55')).toHaveLength(2);
  });

  it('marks metadata-only rows and offers to attach the local file', () => {
    renderWithProviders(<TrackList tracks={[track()]} onAttach={vi.fn()} />);

    expect(screen.getByText('metadata only')).toBeInTheDocument();
    expect(screen.getByText('awaiting enrichment')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Attach file' })).toBeInTheDocument();
  });

  it('marks rows whose audio is already on this device', () => {
    renderWithProviders(
      <TrackList tracks={[track({ mediaurl: 'blob:local-1', isEnriched: true })]} onAttach={vi.fn()} />,
    );

    expect(screen.getByText('on device')).toBeInTheDocument();
    expect(screen.getByText('enriched')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Attach file' })).not.toBeInTheDocument();
  });

  it('calls onAttach with the clicked track', async () => {
    const onAttach = vi.fn();
    const first = track();

    renderWithProviders(<TrackList tracks={[first]} onAttach={onAttach} />);

    await userEvent.click(screen.getByRole('button', { name: 'Attach file' }));

    expect(onAttach).toHaveBeenCalledWith(first);
  });

  it('links rows to their album when a lookup is provided', () => {
    renderWithProviders(
      <TrackList
        tracks={[track()]}
        onAttach={vi.fn()}
        albumFor={() => 'queen/a-night-at-the-opera'}
      />,
    );

    expect(screen.getByRole('link', { name: 'A Night at the Opera' })).toHaveAttribute(
      'href',
      '/album/queen%2Fa-night-at-the-opera',
    );
  });
});
