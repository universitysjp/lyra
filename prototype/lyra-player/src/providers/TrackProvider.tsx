import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';

import TrackContext from '../contexts/TrackContext';
import type { ITrackContext } from '../contexts/TrackContext';

import type { ITrack } from '../types/types';

interface IProps {
  children: ReactNode;
}

/**
 * Playback engine, adapted from the upstream react-music-player template.
 * Lyra only stores metadata, so a track can play once the user attaches the matching
 * file on this device; the audio itself is never uploaded.
 */
const TrackProvider: React.FC<IProps> = ({ children }) => {
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const [volume, setVolume] = useState<number>(0.5);
  const [muted, setMuted] = useState<boolean>(false);
  const [lastVolume, setLastVolume] = useState<number>(0.5);
  const [trackDuration, setTrackDuration] = useState<number>(0);
  const [currentProgress, setCurrentProgress] = useState<number>(0);
  const [currentState, setCurrentState] = useState<string | null>(null);
  const [missingMedia, setMissingMedia] = useState<ITrack | null>(null);
  const [queue, setQueue] = useState<ITrack[]>([]);
  const [currentTrack, setCurrentTrack] = useState<ITrack | null>(null);

  const index = currentTrack ? queue.findIndex((item) => item.id === currentTrack.id) : -1;
  const prevTrack = index > 0 ? queue[index - 1] : null;
  const nextTrack = index >= 0 && index < queue.length - 1 ? queue[index + 1] : null;

  /** Starts a track; without a local file Lyra has nothing to decode. */
  const playTrack = useCallback((track: ITrack, items: ITrack[]): void => {
    if (!track.mediaurl) {
      setMissingMedia(track);

      return;
    }

    setMissingMedia(null);
    setQueue(items);
    setCurrentTrack(track);
    setTrackDuration(track.durationSeconds);
    setCurrentProgress(0);
  }, []);

  /** jsdom and older browsers may return undefined from play(); never let it throw. */
  const attemptPlay = (audio: HTMLAudioElement): void => {
    const started = audio.play();

    if (started && typeof started.catch === 'function') {
      started.catch(() => setCurrentState('paused'));
    }
  };

  const toggleTrack = useCallback(
    (track: ITrack, items: ITrack[]): void => {
      const audio = audioRef.current;

      if (currentTrack?.id === track.id && audio) {
        if (currentState === 'playing') {
          audio.pause();

          return;
        }

        attemptPlay(audio);

        return;
      }

      playTrack(track, items);
    },
    [currentTrack?.id, currentState, playTrack],
  );

  const playNext = useCallback((): void => {
    if (nextTrack) {
      playTrack(nextTrack, queue);
    }
  }, [nextTrack, playTrack, queue]);

  const playPrevious = useCallback((): void => {
    if (prevTrack) {
      playTrack(prevTrack, queue);
    }
  }, [playTrack, prevTrack, queue]);

  // Load and start whenever a different track (or a newly attached file) is selected.
  useEffect(() => {
    const audio = audioRef.current;

    if (!audio || !currentTrack?.mediaurl) {
      return;
    }

    audio.src = currentTrack.mediaurl;
    audio.volume = volume;
    audio.load();

    attemptPlay(audio);
    // Re-runs when the track or its local object URL changes.
  }, [currentTrack?.id, currentTrack?.mediaurl]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleVolumeChange = useCallback((volumeValue: number): void => {
    const audio = audioRef.current;

    setVolume(volumeValue);

    if (audio) {
      audio.volume = volumeValue;
    }

    setMuted(volumeValue === 0);

    if (volumeValue > 0) {
      setLastVolume(volumeValue);
    }
  }, []);

  const handleMuteChange = useCallback((): void => {
    const audio = audioRef.current;

    if (!audio) {
      return;
    }

    if (muted) {
      setMuted(false);
      setVolume(lastVolume);
      audio.volume = lastVolume;

      return;
    }

    setLastVolume(audio.volume || 0.5);
    setMuted(true);
    setVolume(0);
    audio.volume = 0;
  }, [lastVolume, muted]);

  const handleProgressChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>): void => {
      const audio = audioRef.current;

      if (!audio) {
        return;
      }

      const value = event.currentTarget.valueAsNumber;

      setCurrentProgress(value);
      audio.currentTime = value;
    },
    [],
  );

  const providerValue = useMemo<ITrackContext>(
    () => ({
      muted,
      volume,
      audioRef,
      prevTrack,
      nextTrack,
      currentState,
      currentTrack,
      queue,
      trackDuration,
      currentProgress,
      missingMedia,
      playTrack,
      toggleTrack,
      playNext,
      playPrevious,
      handleMuteChange,
      handleVolumeChange,
      handleProgressChange,
      clearMissingMedia: () => setMissingMedia(null),
    }),
    [
      muted,
      volume,
      prevTrack,
      nextTrack,
      currentState,
      currentTrack,
      queue,
      trackDuration,
      currentProgress,
      missingMedia,
      playTrack,
      toggleTrack,
      playNext,
      playPrevious,
      handleMuteChange,
      handleVolumeChange,
      handleProgressChange,
    ],
  );

  return (
    <TrackContext value={providerValue}>
      <audio
        ref={audioRef}
        preload='metadata'
        onEnded={playNext}
        onPause={() => setCurrentState('paused')}
        onPlaying={() => setCurrentState('playing')}
        onCanPlay={(event) => {
          event.currentTarget.volume = volume;
        }}
        onLoadedMetadata={(event) => {
          const duration = event.currentTarget.duration;

          if (Number.isFinite(duration) && duration > 0) {
            setTrackDuration(duration);
          }
        }}
        onDurationChange={(event) => setTrackDuration(event.currentTarget.duration)}
        onTimeUpdate={(event) => setCurrentProgress(event.currentTarget.currentTime)}
      >
        <track kind='captions' />
        Your browser does not support the audio tag.
      </audio>
      {children}
    </TrackContext>
  );
};

export default TrackProvider;
