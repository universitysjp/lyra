import { createContext } from 'react';

import type { TrackMetadataItem } from '../types/lyra';
import type { IAlbumGroup, ITrack } from '../types/types';

export interface ILibraryContext {
  /** Current backend page of the caller's library. */
  tracks: ITrack[];
  albums: IAlbumGroup[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  loading: boolean;
  importing: boolean;
  error: string | null;
  lastImport: { submitted: number; imported: number; duplicates: number } | null;
  goToPage: (page: number) => void;
  reload: () => void;
  /** Loads the whole library once (max page size) so the album grid can group it. */
  loadAlbums: () => Promise<IAlbumGroup[]>;
  importTracks: (items: TrackMetadataItem[]) => Promise<{ submitted: number; imported: number; duplicates: number } | null>;
  /** Registers a locally picked audio file for a track so it becomes playable. */
  attachAudio: (localTrackId: string, file: File) => void;
}

const LibraryContext = createContext<ILibraryContext>({
  tracks: [],
  albums: [],
  page: 1,
  pageSize: 50,
  totalCount: 0,
  totalPages: 0,
  loading: false,
  importing: false,
  error: null,
  lastImport: null,
  goToPage: () => undefined,
  reload: () => undefined,
  loadAlbums: async () => [],
  importTracks: async () => null,
  attachAudio: () => undefined,
});

export default LibraryContext;
