import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';

import { describeError } from '../api/errors';
import { groupByAlbum, toTracks, withLocalAudio } from '../api/mappers';
import LibraryContext from '../contexts/LibraryContext';
import type { ILibraryContext } from '../contexts/LibraryContext';
import { attachLocalAudio } from '../utils/localAudioStore';
import useApi from '../hooks/useApi';
import useAuth from '../hooks/useAuth';
import useLocalAudioVersion from '../hooks/useLocalAudioVersion';

import type { TrackDto, TrackMetadataItem } from '../types/lyra';
import type { IAlbumGroup, ITrack } from '../types/types';

/** Backend defaults (Lyra.Catalog.Api CatalogOptions): default page size 50, max 200. */
export const LIBRARY_PAGE_SIZE = 50;
export const ALBUM_PAGE_SIZE = 200;

interface IProps {
  children: ReactNode;
}

/**
 * Loads the caller's catalog page, posts local metadata batches, and keeps the album
 * grouping in sync. Reads always go through the gateway so Redis cache behaviour applies.
 */
const LibraryProvider: React.FC<IProps> = ({ children }) => {
  const { catalog } = useApi();
  const { status } = useAuth();

  const [dtos, setDtos] = useState<TrackDto[]>([]);
  const [albumDtos, setAlbumDtos] = useState<TrackDto[]>([]);
  const [page, setPage] = useState<number>(1);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(false);
  const [importing, setImporting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [lastImport, setLastImport] = useState<ILibraryContext['lastImport']>(null);
  const [revision, setRevision] = useState<number>(0);
  const [albumsLoaded, setAlbumsLoaded] = useState<boolean>(false);

  // Attaching a local file changes which rows are playable without touching the backend.
  const audioVersion = useLocalAudioVersion();

  useEffect(() => {
    if (status !== 'authenticated') {
      setDtos([]);
      setAlbumDtos([]);
      setTotalCount(0);
      setAlbumsLoaded(false);
      setLastImport(null);
      setPage(1);
    }
  }, [status]);

  useEffect(() => {
    if (status !== 'authenticated') {
      return;
    }

    const controller = new AbortController();

    setLoading(true);
    setError(null);

    catalog
      .listTracks(page, LIBRARY_PAGE_SIZE, controller.signal)
      .then((result) => {
        setDtos(result.items);
        setTotalCount(result.totalCount);
        setPage(result.page);
      })
      .catch((caught: unknown) => setError(describeError(caught)))
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      });

    return () => controller.abort();
  }, [catalog, status, page, revision]);

  const tracks = useMemo<ITrack[]>(() => withLocalAudio(toTracks(dtos)), [dtos, audioVersion]);

  const albums = useMemo<IAlbumGroup[]>(
    () => groupByAlbum(withLocalAudio(toTracks(albumDtos.length > 0 ? albumDtos : dtos))),
    [albumDtos, dtos, audioVersion],
  );

  const loadAlbums = useCallback(
    async (): Promise<IAlbumGroup[]> => {
      if (albumsLoaded) {
        return albums;
      }

      const result = await catalog.listTracks(1, ALBUM_PAGE_SIZE);

      setAlbumDtos(result.items);
      setAlbumsLoaded(true);

      return groupByAlbum(withLocalAudio(toTracks(result.items)));
    },
    [albums, albumsLoaded, catalog],
  );

  const importTracks = useCallback(
    async (items: TrackMetadataItem[]) => {
      if (items.length === 0) {
        setError('Nothing to import: pick at least one audio file.');

        return null;
      }

      setImporting(true);
      setError(null);

      try {
        const result = await catalog.importTracks(items);

        setLastImport(result);
        setAlbumsLoaded(false);
        setRevision((value) => value + 1);

        return result;
      } catch (caught: unknown) {
        setError(describeError(caught));

        return null;
      } finally {
        setImporting(false);
      }
    },
    [catalog],
  );

  const attachAudio = useCallback((localTrackId: string, file: File): void => {
    attachLocalAudio(localTrackId, file);
    setAlbumsLoaded(false);
  }, []);

  const lastPage = Math.max(1, Math.ceil(totalCount / LIBRARY_PAGE_SIZE));

  const value = useMemo<ILibraryContext>(
    () => ({
      tracks,
      albums,
      page,
      pageSize: LIBRARY_PAGE_SIZE,
      totalCount,
      totalPages: lastPage,
      loading,
      importing,
      error,
      lastImport,
      goToPage: (next: number) => setPage(Math.max(1, Math.min(next, lastPage))),
      reload: () => {
        setAlbumsLoaded(false);
        setRevision((current) => current + 1);
      },
      loadAlbums,
      importTracks,
      attachAudio,
    }),
    [
      tracks,
      albums,
      page,
      totalCount,
      lastPage,
      loading,
      importing,
      error,
      lastImport,
      loadAlbums,
      importTracks,
      attachAudio,
    ],
  );

  return <LibraryContext value={value}>{children}</LibraryContext>;
};

export default LibraryProvider;
