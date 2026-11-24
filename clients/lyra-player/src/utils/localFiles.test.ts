import { describe, expect, it } from 'vite-plus/test';

import { isImportable, probeDuration, readLocalTrack, toLocalTrackId } from './localFiles';
import { taggedHeader } from '../test/id3Fixture';

import type { LocalTrack } from './localFiles';

const makeFile = (overrides: Partial<File>): File =>
  ({ name: '', size: 0, lastModified: 0, ...overrides }) as File;

const options = (header: Uint8Array, duration = 355) => ({
  readHeader: async () => header,
  createObjectUrl: () => 'blob:fixture',
  probeDuration: async () => duration,
});

describe('readLocalTrack', () => {
  it('keeps tag values when the file has ID3 metadata', async () => {
    const file = makeFile({ name: 'track-01.mp3', size: 4_521_003, lastModified: 1_700_000_000 });

    const track: LocalTrack = await readLocalTrack(
      file,
      options(
        taggedHeader({
          title: 'Bohemian Rhapsody',
          artist: 'Queen',
          album: 'A Night at the Opera',
          track: '11',
        }),
      ),
    );

    expect(track).toMatchObject({
      localTrackId: toLocalTrackId(file.name, file.size, file.lastModified),
      fileName: 'track-01.mp3',
      title: 'Bohemian Rhapsody',
      artist: 'Queen',
      album: 'A Night at the Opera',
      trackNumber: 11,
      durationSeconds: 355,
      source: 'id3',
      objectUrl: 'blob:fixture',
    });
  });

  it('falls back to the file name when there are no tags', async () => {
    const file = makeFile({ name: 'Queen - Bohemian Rhapsody.mp3', size: 10, lastModified: 0 });

    const track = await readLocalTrack(file, options(new Uint8Array(0), 209));

    expect(track.title).toBe('Bohemian Rhapsody');
    expect(track.artist).toBe('Queen');
    expect(track.album).toBeNull();
    expect(track.source).toBe('filename');
    expect(track.localTrackId).toBe('Queen - Bohemian Rhapsody-10-0');
  });

  it('passes the object URL to the duration probe', async () => {
    const probed: string[] = [];
    const file = makeFile({ name: 'a.m4a', size: 1, lastModified: 0 });

    await readLocalTrack(file, {
      readHeader: async () => new Uint8Array(0),
      createObjectUrl: () => 'blob:probed',
      probeDuration: async (url: string) => {
        probed.push(url);

        return 199;
      },
    });

    expect(probed).toEqual(['blob:probed']);
  });
});

describe('toLocalTrackId', () => {
  it('strips the extension and includes size plus modified time', () => {
    expect(toLocalTrackId('Sultans Of Swing.flac', 32_114_022, 1_700_000_000)).toBe(
      'Sultans Of Swing-32114022-1700000000',
    );
    expect(toLocalTrackId('unknown-size.mp3', 5, 0)).toBe('unknown-size-5-0');
  });
});

describe('isImportable', () => {
  const base: LocalTrack = {
    localTrackId: 'id',
    fileName: 'f.mp3',
    title: 'Title',
    artist: 'Artist',
    album: null,
    trackNumber: null,
    durationSeconds: 200,
    source: 'id3',
    objectUrl: 'blob:x',
    file: makeFile({ name: 'f.mp3' }),
  };

  it('accepts complete metadata', () => {
    expect(isImportable(base)).toBe(true);
  });

  it('rejects rows the backend would refuse', () => {
    expect(isImportable({ ...base, title: '   ' })).toBe(false);
    expect(isImportable({ ...base, artist: '' })).toBe(false);
    expect(isImportable({ ...base, durationSeconds: 0 })).toBe(false);
  });
});

describe('probeDuration', () => {
  it('resolves zero for media the browser cannot decode', async () => {
    await expect(probeDuration('blob:not-audio', 20)).resolves.toBe(0);
  });
});
