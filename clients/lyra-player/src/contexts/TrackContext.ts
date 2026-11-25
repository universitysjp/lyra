import { createContext } from 'react';

import type { ITrack } from '../types/types';

export interface ITrackContext {
  muted: boolean;
  volume: number;
  audioRef?: React.RefObject<HTMLAudioElement | null> | null;
  prevTrack: ITrack | null;
  nextTrack: ITrack | null;
  currentState?: string | null;
  currentTrack?: ITrack | null;
  /** The list playback walks through, i.e. the library page, search result or album. */
  queue?: ITrack[];
  trackDuration: number;
  currentProgress: number;
  /** Set when a track was clicked but its audio file is not on this device yet. */
  missingMedia: ITrack | null;
  playTrack: (track: ITrack, queue: ITrack[]) => void;
  toggleTrack: (track: ITrack, queue: ITrack[]) => void;
  playNext: () => void;
  playPrevious: () => void;
  handleMuteChange: () => void;
  handleVolumeChange: (volumeValue: number) => void;
  handleProgressChange: (event: React.ChangeEvent<HTMLInputElement>) => void;
  clearMissingMedia: () => void;
}

export const DEFAULT_TRACK_CONTEXT: ITrackContext = {
  muted: false,
  volume: 0.5,
  audioRef: null,
  prevTrack: null,
  nextTrack: null,
  currentState: null,
  currentTrack: null,
  queue: [],
  trackDuration: 0,
  currentProgress: 0,
  missingMedia: null,
  playTrack: () => undefined,
  toggleTrack: () => undefined,
  playNext: () => undefined,
  playPrevious: () => undefined,
  handleMuteChange: () => undefined,
  handleVolumeChange: () => undefined,
  handleProgressChange: () => undefined,
  clearMissingMedia: () => undefined,
};

const TrackContext = createContext<ITrackContext>(DEFAULT_TRACK_CONTEXT);

export default TrackContext;
