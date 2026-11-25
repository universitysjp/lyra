import type { LyraClient } from './http';
import type { GeneratePlaylistRequest, PlaylistDto } from '../types/lyra';

export interface PlaylistsApi {
  generate(request: GeneratePlaylistRequest, signal?: AbortSignal): Promise<PlaylistDto>;
}

/** Lyra.PlaylistRag.Api endpoints exposed through the gateway. */
export const createPlaylistsApi = (client: LyraClient): PlaylistsApi => ({
  generate(request, signal) {
    return client.request<PlaylistDto>('/api/playlists/generate', {
      method: 'POST',
      body: request,
      signal,
    });
  },
});
