import type { LyraClient } from './http';
import type {
  ImportTracksResponse,
  PagedResponse,
  TrackDto,
  TrackMetadataItem,
} from '../types/lyra';

export interface CatalogApi {
  listTracks(page: number, pageSize: number, signal?: AbortSignal): Promise<PagedResponse<TrackDto>>;
  searchTracks(query: string, signal?: AbortSignal): Promise<TrackDto[]>;
  importTracks(tracks: TrackMetadataItem[], signal?: AbortSignal): Promise<ImportTracksResponse>;
}

/** Lyra.Catalog.Api endpoints exposed through the gateway. */
export const createCatalogApi = (client: LyraClient): CatalogApi => ({
  listTracks(page, pageSize, signal) {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });

    return client.request<PagedResponse<TrackDto>>(`/api/catalog/tracks?${params}`, { signal });
  },

  searchTracks(query, signal) {
    const params = new URLSearchParams({ q: query });

    return client.request<TrackDto[]>(`/api/catalog/tracks/search?${params}`, { signal });
  },

  importTracks(tracks, signal) {
    return client.request<ImportTracksResponse>('/api/catalog/tracks/batch', {
      method: 'POST',
      body: { tracks },
      signal,
    });
  },
});
