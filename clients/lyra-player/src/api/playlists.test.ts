import { beforeEach, describe, expect, it } from 'vite-plus/test';

import { createAuthApi } from './auth';
import { createLyraClient } from './http';
import { createPlaylistsApi } from './playlists';
import { createMemorySessionStore } from './session';
import { createFakeBackend, DEMO_EMAIL, DEMO_PASSWORD, seedTracks } from '../test/fakeBackend';

describe('playlists api against the fake gateway', () => {
  let backend: ReturnType<typeof createFakeBackend>;
  let playlists: ReturnType<typeof createPlaylistsApi>;

  beforeEach(async () => {
    backend = createFakeBackend({ tracks: seedTracks });

    const client = createLyraClient({
      store: createMemorySessionStore(),
      fetchImpl: backend.fetchImpl,
    });

    await createAuthApi(client).login(DEMO_EMAIL, DEMO_PASSWORD);
    backend.seedAiConnection();
    playlists = createPlaylistsApi(client);
  });

  it('generates a playlist of the requested size from the local library', async () => {
    const playlist = await playlists.generate({ prompt: 'late night drive', limit: 1 });

    expect(playlist.title).toBe('late night drive');
    expect(playlist.description).toMatch(/your own library/);
    expect(playlist.tracks).toHaveLength(1);
    expect(playlist.tracks[0]?.localTrackId).toMatch(/^(bohemian|sultans)-/);
    expect(backend.requests).toContain('POST /api/playlists/generate');
    expect(backend.playlistRequests).toEqual([{ prompt: 'late night drive', limit: 1 }]);
  });

  it('requires an AI connection', async () => {
    backend.aiConnections.clear();

    await expect(playlists.generate({ prompt: 'quiet morning' })).rejects.toMatchObject({
      status: 409,
      message: expect.stringContaining('Connect an AI provider'),
    });
  });

  it('validates the prompt and limit before generating', async () => {
    await expect(playlists.generate({ prompt: '   ' })).rejects.toMatchObject({ status: 400 });
    await expect(playlists.generate({ prompt: 'valid', limit: 0 })).rejects.toMatchObject({
      status: 400,
    });
  });

  it('requires a bearer token', async () => {
    const anonymous = createPlaylistsApi(
      createLyraClient({ store: createMemorySessionStore(), fetchImpl: backend.fetchImpl }),
    );

    await expect(anonymous.generate({ prompt: 'quiet morning' })).rejects.toThrow(/Access token/);
  });
});
