import { beforeEach, describe, expect, it } from 'vite-plus/test';

import { createAuthApi } from './auth';
import { ApiError } from './http';
import { createCatalogApi } from './catalog';
import { createFakeBackend, DEMO_EMAIL, DEMO_PASSWORD, seedTracks } from '../test/fakeBackend';
import { createLyraClient } from './http';
import { createMemorySessionStore } from './session';

import type { TrackMetadataItem } from '../types/lyra';

const batch = (offset = 0): TrackMetadataItem[] =>
  seedTracks.map((item) => ({
    ...item,
    localTrackId: offset === 0 ? item.localTrackId : `${item.localTrackId}-${offset}`,
    title: offset === 0 ? item.title : `${item.title} (${offset})`,
  }));

describe('catalog api against the fake gateway', () => {
  let backend: ReturnType<typeof createFakeBackend>;
  let catalog: ReturnType<typeof createCatalogApi>;

  beforeEach(async () => {
    backend = createFakeBackend();

    const client = createLyraClient({
      store: createMemorySessionStore(),
      fetchImpl: backend.fetchImpl,
    });

    await createAuthApi(client).login(DEMO_EMAIL, DEMO_PASSWORD);

    catalog = createCatalogApi(client);
  });

  it('accepts a batch and reports what was new', async () => {
    const result = await catalog.importTracks(batch());

    expect(result).toEqual({ submitted: 2, imported: 2, duplicates: 0 });
    expect(backend.requests).toContain('POST /api/catalog/tracks/batch');
    expect(backend.tracks).toHaveLength(2);
  });

  it('deduplicates a repeated import of the same files', async () => {
    const items = batch(1);

    await catalog.importTracks(items);
    const repeat = await catalog.importTracks(items);

    expect(repeat).toEqual({ submitted: 2, imported: 0, duplicates: 2 });
    expect(backend.tracks).toHaveLength(2);
  });

  it('rejects an empty batch without touching the catalog', async () => {
    await expect(catalog.importTracks([])).rejects.toThrow(/at least one track/);
    expect(backend.tracks).toHaveLength(0);
  });

  it('rejects a batch larger than 500 items', async () => {
    const items = Array.from({ length: 501 }, (_, index) => ({
      localTrackId: `local-${index}`,
      title: `Title ${index}`,
      artist: 'Artist',
      album: null,
      durationSeconds: 200,
    }));

    await expect(catalog.importTracks(items)).rejects.toThrow(/maximum allowed/);
    expect(backend.tracks).toHaveLength(0);
  });

  it('refuses items with missing title or artist', async () => {
    const error = await catalog
      .importTracks([
        { localTrackId: 'x', title: '  ', artist: 'Queen', album: null, durationSeconds: 100 },
      ])
      .catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect(backend.tracks).toHaveLength(0);
  });

  it('lists the library newest first with paging metadata', async () => {
    await catalog.importTracks(batch());

    const page = await catalog.listTracks(1, 1);

    expect(page).toMatchObject({ page: 1, pageSize: 1, totalCount: 2 });
    expect(page.items).toHaveLength(1);
    expect(page.items[0].title).toBe('Sultans of Swing');

    const second = await catalog.listTracks(2, 1);

    expect(second.items[0].title).toBe('Bohemian Rhapsody');
  });

  it('clamps the page size to the backend maximum', async () => {
    const page = await catalog.listTracks(1, 5000);

    expect(page.pageSize).toBe(200);
  });

  it('uses the default page size when the caller sends zero', async () => {
    const page = await catalog.listTracks(1, 0);

    expect(page.pageSize).toBe(50);
  });

  it('loads plain and synced lyrics for an owned track', async () => {
    await catalog.importTracks(batch());
    const track = backend.tracks[0];

    backend.setLyrics(track.id, {
      plainLyrics: 'plain line one\nplain line two',
      syncedLyrics: '[00:01.00] synced line one\n[00:05.50] synced line two',
    });

    const lyrics = await catalog.getTrackLyrics(track.id);

    expect(lyrics).toMatchObject({
      trackId: track.id,
      isEnriched: false,
      plainLyrics: 'plain line one\nplain line two',
      syncedLyrics: '[00:01.00] synced line one\n[00:05.50] synced line two',
    });
    expect(backend.requests).toContain(`GET /api/catalog/tracks/${track.id}/lyrics`);
  });

  it('finds a track through fuzzy search despite a typo', async () => {
    await catalog.importTracks(batch());
    const results = await catalog.searchTracks('bohemain rhapsody');

    expect(results).toHaveLength(1);
    expect(results[0].title).toBe('Bohemian Rhapsody');
    expect(backend.requests.some((entry) => entry.includes('q=bohemain+rhapsody'))).toBe(true);
  });

  it('returns nothing for unrelated queries and rejects blanks', async () => {
    await catalog.importTracks(batch());

    expect(await catalog.searchTracks('zzzzzz')).toEqual([]);
    await expect(catalog.searchTracks('   ')).rejects.toThrow(/non-empty search query/);
  });

  it('requires a bearer token for every catalog call', async () => {
    const anonymous = createCatalogApi(
      createLyraClient({ store: createMemorySessionStore(), fetchImpl: backend.fetchImpl }),
    );

    await expect(anonymous.listTracks(1, 50)).rejects.toThrow(/Access token/);
    await expect(anonymous.searchTracks('queen')).rejects.toThrow(/Access token/);
    await expect(anonymous.importTracks(batch())).rejects.toThrow(/Access token/);
  });

  it('keeps libraries separate per user', async () => {
    await catalog.importTracks(batch(1));

    const store = createMemorySessionStore();
    const client = createLyraClient({ store, fetchImpl: backend.fetchImpl });
    const otherCatalog = createCatalogApi(client);

    await createAuthApi(client).register('second@example.com', 'AnotherPassword!');

    const result = await otherCatalog.importTracks(batch(2));
    const otherPage = await otherCatalog.listTracks(1, 50);
    const ownPage = await catalog.listTracks(1, 50);

    expect(result.imported).toBe(2);
    expect(otherPage.totalCount).toBe(2);
    expect(ownPage.totalCount).toBe(2);
    expect(otherPage.items.map((item) => item.id)).not.toEqual(
      ownPage.items.map((item) => item.id),
    );
  });
});
