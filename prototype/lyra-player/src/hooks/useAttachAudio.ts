import { useCallback } from 'react';

import { pickAudioFiles } from '../utils/filePicker';
import { attachLocalAudio } from '../utils/localAudioStore';
import useLibrary from './useLibrary';
import useTrack from './useTrack';

import type { ITrack } from '../types/types';

/**
 * Lets the user point the player at a local file for one metadata-only track, then starts
 * playback with the refreshed queue. The file itself is only read by the browser.
 */
const useAttachAudio = () => {
  const { tracks } = useLibrary();
  const { playTrack } = useTrack();

  return useCallback(
    async (track: ITrack, queue?: ITrack[]): Promise<void> => {
      const [file] = await pickAudioFiles(false);

      if (!file) {
        return;
      }

      const objectUrl = attachLocalAudio(track.localTrackId, file);
      const items = (queue && queue.length > 0 ? queue : tracks).map((item) =>
        item.localTrackId === track.localTrackId ? { ...item, mediaurl: objectUrl } : item,
      );

      const started = items.find((item) => item.id === track.id) ?? {
        ...track,
        mediaurl: objectUrl,
      };

      playTrack(started, items);
    },
    [playTrack, tracks],
  );
};

export default useAttachAudio;
