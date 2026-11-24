import { useCallback, useState } from 'react';

import useLibrary from './useLibrary';

import { pickAudioFiles } from '../utils/filePicker';
import { toLocalTrackId } from '../utils/localFiles';
import { attachLocalAudio } from '../utils/localAudioStore';

export interface ConnectLocalFilesResult {
  matched: number;
  total: number;
}

/**
 * Reconnects audio files that were imported before the browser began persisting them.
 * Files are matched by the same stable local id the importer generated (name + size + mtime).
 */
const useConnectLocalFiles = () => {
  const { tracks, albums, loadAlbums } = useLibrary();
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<ConnectLocalFilesResult | null>(null);

  const connect = useCallback(async (): Promise<void> => {
    const files = await pickAudioFiles(true);

    if (files.length === 0) {
      return;
    }

    setBusy(true);
    setResult(null);

    try {
      let known = albums.length > 0 ? albums.flatMap((album) => album.tracks) : tracks;

      if (albums.length === 0) {
        try {
          known = (await loadAlbums()).flatMap((album) => album.tracks);
        } catch {
          // Fall back to the current page when widening fails.
        }
      }

      const byLocalId = new Map(known.map((track) => [track.localTrackId, track]));
      let matched = 0;

      files.forEach((file) => {
        const localTrackId = toLocalTrackId(file.name, file.size, file.lastModified);

        if (byLocalId.has(localTrackId)) {
          attachLocalAudio(localTrackId, file);
          matched += 1;
        }
      });

      setResult({ matched, total: files.length });
    } finally {
      setBusy(false);
    }
  }, [albums, loadAlbums, tracks]);

  return { connect, busy, result };
};

export default useConnectLocalFiles;
