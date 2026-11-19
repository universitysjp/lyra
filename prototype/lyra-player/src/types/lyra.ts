/**
 * Wire contracts mirroring the Lyra backend DTOs (System.Text.Json camelCase).
 * Identity: Lyra.Identity.Api/DTOs, Catalog: Lyra.Catalog.Api/DTOs.
 */

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresInSeconds: number;
  userId: string;
}

export interface MeResponse {
  userId: string;
  email: string;
  createdAt: string;
}

/** One locally extracted ID3 record, as sent to POST /api/catalog/tracks/batch. */
export interface TrackMetadataItem {
  localTrackId: string;
  title: string;
  artist: string;
  album?: string | null;
  durationSeconds: number;
}

export interface ImportTracksRequest {
  tracks: TrackMetadataItem[];
}

export interface ImportTracksResponse {
  submitted: number;
  imported: number;
  duplicates: number;
}

export interface TrackDto {
  id: string;
  localTrackId: string;
  title: string;
  artist: string;
  album: string | null;
  durationSeconds: number;
  isEnriched: boolean;
  importedAt: string;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
