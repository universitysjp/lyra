import { useState } from 'react';

import Artwork from '../Artwork/Artwork';
import useTrack from '../../hooks/useTrack';
import LyricsPanel from '../Lyrics/LyricsPanel';
import { formatTime } from '../../utils/toolbox';

/** Bottom now-playing bar. */
const Player: React.FC = () => {
  const {
    currentTrack,
    currentState,
    prevTrack,
    nextTrack,
    queue,
    trackDuration,
    currentProgress,
    muted,
    volume,
    toggleTrack,
    playNext,
    playPrevious,
    handleProgressChange,
    handleMuteChange,
    handleVolumeChange,
  } = useTrack();

  const [lyricsOpen, setLyricsOpen] = useState<boolean>(false);

  if (!currentTrack) {
    return null;
  }

  const playing = currentState === 'playing';
  const duration = trackDuration > 0 ? trackDuration : currentTrack.durationSeconds;
  const items = queue && queue.length > 0 ? queue : [currentTrack];

  return (
    <>
      <LyricsPanel open={lyricsOpen} onClose={() => setLyricsOpen(false)} />

      <div className='player no-select'>
        <div className='player-grid'>
          <div className='player-now'>
            <Artwork
              className='player-cover'
              src={currentTrack.artwork}
              seed={`${currentTrack.artist}::${currentTrack.album}`}
              initials={currentTrack.album}
              alt={`${currentTrack.name} artwork`}
            />
            <div className='player-now-info'>
              <strong>{currentTrack.name}</strong>
              <span>{currentTrack.artist}</span>
            </div>
            <button
              type='button'
              className={lyricsOpen ? 'player-lyrics-btn active' : 'player-lyrics-btn'}
              onClick={() => setLyricsOpen((open) => !open)}
              title={lyricsOpen ? 'Hide lyrics' : 'Show lyrics'}
              aria-label={lyricsOpen ? 'Hide lyrics' : 'Show lyrics'}
              aria-pressed={lyricsOpen}
            >
              <span className='material-symbols-outlined' aria-hidden='true'>
                lyrics
              </span>
            </button>
          </div>

          <div className='player-center'>
            <div className='player-controls'>
              <button
                type='button'
                onClick={playPrevious}
                disabled={!prevTrack}
                title={prevTrack ? `Previous: ${prevTrack.name}` : 'No previous track'}
                aria-label='Previous track'
              >
                <span className='material-symbols-outlined' aria-hidden='true'>
                  skip_previous
                </span>
              </button>

              <button
                type='button'
                className='player-play'
                onClick={() => toggleTrack(currentTrack, items)}
                title={playing ? 'Pause' : 'Play'}
                aria-label={playing ? 'Pause' : 'Play'}
              >
                <span className='material-symbols-outlined' aria-hidden='true'>
                  {playing ? 'pause' : 'play_arrow'}
                </span>
              </button>

              <button
                type='button'
                onClick={playNext}
                disabled={!nextTrack}
                title={nextTrack ? `Next: ${nextTrack.name}` : 'No next track'}
                aria-label='Next track'
              >
                <span className='material-symbols-outlined' aria-hidden='true'>
                  skip_next
                </span>
              </button>
            </div>

            <div className='player-time-row'>
              <span>{formatTime(currentProgress)}</span>
              <div className='player-progress'>
                <input
                  type='range'
                  min={0}
                  max={duration || 0}
                  step={0.01}
                  value={Math.min(currentProgress, duration || 0)}
                  aria-label='Seek'
                  onChange={handleProgressChange}
                />
              </div>
              <span>{formatTime(duration)}</span>
            </div>
          </div>

          <div className='player-right'>
            <div className='player-volume'>
              <input
                type='range'
                min={0}
                max={1}
                step={0.05}
                value={muted ? 0 : volume}
                aria-label='Volume'
                onChange={(event) => handleVolumeChange(Number(event.target.value))}
              />
              <button
                type='button'
                onClick={handleMuteChange}
                title={muted ? 'Unmute' : 'Mute'}
                aria-label={muted ? 'Unmute' : 'Mute'}
              >
                <span className='material-symbols-outlined' aria-hidden='true'>
                  {muted ? 'volume_off' : 'volume_up'}
                </span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </>
  );
};

export default Player;
