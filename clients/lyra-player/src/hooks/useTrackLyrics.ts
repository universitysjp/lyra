import { useEffect, useState } from 'react';

import { describeError, isAbortError } from '../api/errors';
import useApi from './useApi';

import type { TrackLyricsDto } from '../types/lyra';

export interface TrackLyricsState {
  loading: boolean;
  error: string | null;
  lyrics: TrackLyricsDto | null;
}

/** Loads the current track's lyrics lazily from the catalog, scoped to the signed-in user. */
const useTrackLyrics = (trackId: string | undefined): TrackLyricsState => {
  const { catalog } = useApi();
  const [state, setState] = useState<TrackLyricsState>({
    loading: false,
    error: null,
    lyrics: null,
  });

  useEffect(() => {
    setState({ loading: false, error: null, lyrics: null });

    if (!trackId) {
      return;
    }

    const controller = new AbortController();

    setState({ loading: true, error: null, lyrics: null });

    catalog
      .getTrackLyrics(trackId, controller.signal)
      .then((lyrics) => {
        if (!controller.signal.aborted) {
          setState({ loading: false, error: null, lyrics });
        }
      })
      .catch((caught: unknown) => {
        if (isAbortError(caught)) {
          return;
        }

        setState({ loading: false, error: describeError(caught), lyrics: null });
      });

    return () => controller.abort();
  }, [catalog, trackId]);

  return state;
};

export default useTrackLyrics;
