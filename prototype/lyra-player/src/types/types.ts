// UI models the player components render. Tracks come from the Lyra catalog API,
// audio always comes from a file the user picked on this device.

export interface ITrack {
  /** Catalog track id (backend `tracks.id`). */
  id: string;
  /** Stable id of the file on this device, used to match metadata with local audio. */
  localTrackId: string;
  /** Position in the currently rendered list. */
  no: number;
  name: string;
  artist: string;
  album: string;
  /** Human readable duration, e.g. `05:55`. */
  duration: string;
  durationSeconds: number;
  isEnriched: boolean;
  hasLyrics: boolean;
  hasSyncedLyrics: boolean;
  importedAt: string;
  /** Object URL of the local audio file, empty until the user attaches one. */
  mediaurl: string;
  /** Deterministic placeholder artwork; Lyra stores metadata, not cover art. */
  artwork: string;
}

export interface IAlbumGroup {
  id: string;
  name: string;
  artist: string;
  songs: number;
  minutes: number;
  image: string;
  tracks: ITrack[];
}

export interface ISearchState {
  query: string;
  results: ITrack[];
  loading: boolean;
  error: string | null;
}
