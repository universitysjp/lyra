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
  hasLyrics: boolean;
  hasSyncedLyrics: boolean;
  importedAt: string;
}

export interface TrackLyricsDto {
  trackId: string;
  isEnriched: boolean;
  plainLyrics: string | null;
  syncedLyrics: string | null;
}

/** POST /api/playlists/generate through the gateway. */
export interface GeneratePlaylistRequest {
  prompt: string;
  limit?: number;
}

/** One generated entry. Audio stays local, so the backend returns LocalTrackId. */
export interface PlaylistTrackDto {
  localTrackId: string;
  title: string;
  artist: string;
  album: string | null;
  durationSeconds: number;
  reason: string | null;
}

export interface PlaylistDto {
  title: string;
  description: string;
  tracks: PlaylistTrackDto[];
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** Provider values match Lyra.Contracts.AI.AiProvider and use string JSON enum serialization. */
export type AiProvider = 'OpenAI' | 'Gemini' | 'OpenCode' | 'Anthropic';

export type OpenCodePlan = 'PayAsYouGo' | 'Go';

/** Metadata returned by Identity; the API key is deliberately never included. */
export interface AiConnectionDto {
  provider: AiProvider;
  model: string;
  openCodePlan: OpenCodePlan | null;
  createdAt: string;
  updatedAt: string;
}

export interface UpsertAiConnectionRequest {
  provider: AiProvider;
  model: string;
  apiKey: string;
  openCodePlan: OpenCodePlan | null;
}
