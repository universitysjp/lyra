import { useEffect, useMemo, useRef } from 'react';

import useTrack from '../../hooks/useTrack';
import useTrackLyrics from '../../hooks/useTrackLyrics';
import {
  activeLyricIndex,
  parseSyncedLyrics,
  splitPlainLyrics,
} from '../../utils/lyrics';

interface IProps {
  open: boolean;
  onClose: () => void;
}

/**
 * Spotify-style lyrics drawer. Synced lyrics highlight and scroll with playback;
 * plain lyrics are shown as static text when no timestamped version exists.
 */
const LyricsPanel: React.FC<IProps> = ({ open, onClose }) => {
  const { currentTrack, currentProgress, audioRef } = useTrack();
  const { loading, error, lyrics } = useTrackLyrics(open ? currentTrack?.id : undefined);

  const lines = useMemo(() => {
    const synced = parseSyncedLyrics(lyrics?.syncedLyrics);

    return synced.length > 0 ? synced : splitPlainLyrics(lyrics?.plainLyrics);
  }, [lyrics]);

  const isSynced = lines.some((line) => line.timeSeconds !== null);
  const activeIndex = isSynced ? activeLyricIndex(lines, currentProgress) : -1;
  const activeRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    if (open && activeRef.current?.scrollIntoView) {
      activeRef.current.scrollIntoView({ block: 'center', behavior: 'smooth' });
    }
  }, [activeIndex, open]);

  if (!open || !currentTrack) {
    return null;
  }

  const seek = (timeSeconds: number | null): void => {
    const audio = audioRef?.current;

    if (audio && timeSeconds !== null) {
      audio.currentTime = timeSeconds;
    }
  };

  const renderBody = (): React.ReactNode => {
    if (loading) {
      return <p className='lyrics-empty'>Loading lyrics...</p>;
    }

    if (error) {
      return <p className='lyrics-empty'>{error}</p>;
    }

    if (lines.length === 0) {
      return (
        <p className='lyrics-empty'>
          {currentTrack.isEnriched
            ? 'No lyrics were found for this track.'
            : 'Lyrics will appear here after enrichment finishes.'}
        </p>
      );
    }

    return lines.map((line, index) =>
      line.timeSeconds === null ? (
        <p key={`plain-${index}`} className='lyrics-line lyrics-line-plain'>
          {line.text}
        </p>
      ) : (
        <button
          key={`${line.timeSeconds}-${index}`}
          ref={index === activeIndex ? activeRef : undefined}
          type='button'
          className={
            index === activeIndex
              ? 'lyrics-line lyrics-line-synced lyrics-line-active'
              : 'lyrics-line lyrics-line-synced'
          }
          onClick={() => seek(line.timeSeconds)}
          title='Jump to this line'
        >
          {line.text}
        </button>
      ),
    );
  };

  return (
    <aside className='lyrics-panel' aria-label={`Lyrics for ${currentTrack.name}`}>
      <header className='lyrics-header flex flex-space-between flex-v-center'>
        <div className='flex flex-column'>
          <strong>Lyrics</strong>
          <span className='muted small'>
            {currentTrack.name} &bull; {currentTrack.artist}
          </span>
        </div>
        <button
          type='button'
          className='icon-btn'
          onClick={onClose}
          title='Close lyrics'
          aria-label='Close lyrics'
        >
          <span className='material-symbols-outlined' aria-hidden='true'>
            close
          </span>
        </button>
      </header>
      <div className='lyrics-scroll'>{renderBody()}</div>
      {isSynced && (
        <footer className='lyrics-footer muted small'>
          Tap a line to jump to it.
        </footer>
      )}
    </aside>
  );
};

export default LyricsPanel;
