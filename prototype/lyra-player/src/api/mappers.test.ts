import { describe, expect, it } from 'vitest';

import {
  groupByAlbum,
  toMetadataItems,
  toTrack,
  toTracks,
  totalSeconds,
  withLocalAudio,
} from './mappers';
import { registerLocalAudio } from '../utils/localAudioStore';

import type { TrackDto } from '../types/lyra';
import type { LocalTrack } from '../utils/localFiles';

const dto = (overrides: Partial<TrackDto> = {}): TrackDto => ({
  id: 'track-1',
  localTrackId: 'bohemian-45210034-0',
  title: 'Bohemian Rhapsody',
  artist: 'Queen',
  album: 'A Night at the Opera',
  durationSeconds: 355,
  isEnriched: false,
  importedAt: '2026-11-17T10:00:00Z',
  ...overrides,
});

describe('toTrack', () => {
  it('maps catalog fields onto the player model', () => {
    const track = toTrack(dto(), 3);

    expect(track).toMatchObject({
      id: 'track-1',
      no: 3,
      name: 'Bohemian Rhapsody',
      artist: 'Queen',
      album: 'A Night at the Opera',
      duration: '05:55',
      durationSeconds: 355,
      isEnriched: false,
      mediaurl: '',
    });
    expect(track.artwork).toContain('data:image/svg+xml');
  });

  it('labels missing albums as singles and keeps local ids for playback matching', () => {
    const track = toTrack(dto({ album: null }), 1);

    expect(track.album).toBe('Singles');
    expect(track.localTrackId).toBe('bohemian-45210034-0');
  });

  it('picks up an attached local file from the registry', () => {
    registerLocalAudio('sultans-32114022-0', 'blob:sultans');

    const track = toTrack(dto({ localTrackId: 'sultans-32114022-0' }), 1);

    expect(track.mediaurl).toBe('blob:sultans');
  });
});

describe('toTracks', () => {
  it('numbers rows in the order the backend returned them', () => {
    const tracks = toTracks([
      dto({ id: 'a', title: 'First' }),
      dto({ id: 'b', title: 'Second' }),
    ]);

    expect(tracks.map((track) => [track.no, track.name])).toEqual([
      [1, 'First'],
      [2, 'Second'],
    ]);
  });
});

describe('withLocalAudio', () => {
  it('refreshes only the rows that gained a file', () => {
    const tracks = toTracks([dto({ id: 'a' }), dto({ id: 'b', localTrackId: 'other-1-0' })]);

    registerLocalAudio('bohemian-45210034-0', 'blob:a');

    const refreshed = withLocalAudio(tracks);

    expect(refreshed[0].mediaurl).toBe('blob:a');
    expect(refreshed[1].mediaurl).toBe('');
    expect(refreshed[1]).toBe(tracks[1]);
  });
});

describe('groupByAlbum', () => {
  const tracks = toTracks([
    dto({ id: 'a', title: 'Bohemian Rhapsody', durationSeconds: 355 }),
    dto({ id: 'b', title: "Don't Stop Me Now", durationSeconds: 209 }),
    dto({
      id: 'c',
      title: 'Sultans of Swing',
      artist: 'Dire Straits',
      album: 'Dire Straits',
      durationSeconds: 348,
      localTrackId: 'sultans-1-0',
    }),
  ]);

  it('groups by artist and album with counts and rounded minutes', () => {
    const albums = groupByAlbum(tracks);

    expect(albums).toHaveLength(2);
    expect(albums[0]).toMatchObject({
      artist: 'Dire Straits',
      name: 'Dire Straits',
      songs: 1,
      minutes: 6,
    });
    expect(albums[1]).toMatchObject({
      id: 'queen/a-night-at-the-opera',
      artist: 'Queen',
      name: 'A Night at the Opera',
      songs: 2,
      minutes: 9,
    });
    expect(albums[1].tracks.map((track) => track.id)).toEqual(['a', 'b']);
  });

  it('returns nothing for an empty library', () => {
    expect(groupByAlbum([])).toEqual([]);
  });
});

describe('toMetadataItems', () => {
  it('builds the batch payload from locally extracted tracks', () => {
    const local = {
      localTrackId: 'queen - bohemian rhapsody-45210034-0',
      fileName: 'Queen - Bohemian Rhapsody.mp3',
      title: 'Bohemian Rhapsody',
      artist: 'Queen',
      album: null,
      trackNumber: null,
      durationSeconds: 354.7,
      source: 'id3',
      objectUrl: 'blob:1',
      file: {} as File,
    } satisfies LocalTrack;

    expect(toMetadataItems([local])).toEqual([
      {
        localTrackId: 'queen - bohemian rhapsody-45210034-0',
        title: 'Bohemian Rhapsody',
        artist: 'Queen',
        album: null,
        durationSeconds: 354.7,
      },
    ]);
  });
});

describe('totalSeconds', () => {
  it('sums durations for the library header', () => {
    expect(totalSeconds(toTracks([dto({ durationSeconds: 100 }), dto({ id: 'z', durationSeconds: 200 })]))).toBe(300);
    expect(totalSeconds([])).toBe(0);
  });
});
