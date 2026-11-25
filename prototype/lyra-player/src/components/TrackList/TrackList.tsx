import { Link } from 'react-router-dom';

import Artwork from '../Artwork/Artwork';
import useTrack from '../../hooks/useTrack';

import type { ITrack } from '../../types/types';

interface IProps {
  tracks: ITrack[];
  onAttach: (track: ITrack) => void;
  albumFor?: (track: ITrack) => string;
  reasonFor?: (track: ITrack) => string | null;
  emptyLabel?: string;
  showAlbum?: boolean;
}

/** The shared track table used by the library, search results and album pages. */
const TrackList: React.FC<IProps> = ({
  tracks,
  onAttach,
  albumFor,
  reasonFor,
  emptyLabel = 'Nothing here yet.',
  showAlbum = true,
}) => {
  const { currentTrack, currentState, toggleTrack } = useTrack();

  if (tracks.length === 0) {
    return <p className='empty'>{emptyLabel}</p>;
  }

  return (
    <div className='track-table' role='list'>
      <div className='track-head' aria-hidden='true'>
        <span />
        <span>Title</span>
        {showAlbum ? <span>Album</span> : <span />}
        <span />
        <span className='track-time'>Time</span>
      </div>

      {tracks.map((track) => {
        const selected = currentTrack?.id === track.id;
        const playing = selected && currentState === 'playing';
        const reason = reasonFor?.(track);

        return (
          <div
            key={track.id}
            role='listitem'
            className={selected ? 'track-row is-current' : 'track-row'}
          >
            <button
              type='button'
              className='track-play'
              title={playing ? `Pause ${track.name}` : `Play ${track.name}`}
              aria-label={playing ? `Pause ${track.name}` : `Play ${track.name}`}
              onClick={() => toggleTrack(track, tracks)}
            >
              <Artwork
                className='track-art'
                src={track.artwork}
                seed={`${track.artist}::${track.album}`}
                initials={track.album}
                alt={`${track.name} artwork`}
              />
              <span className='track-play-icon' aria-hidden='true'>
                <span className='track-play-badge'>
                  <span className='material-symbols-outlined'>
                    {playing ? 'pause' : 'play_arrow'}
                  </span>
                </span>
              </span>
            </button>

            <div className='track-main'>
              <button
                type='button'
                className='track-name'
                onClick={() => toggleTrack(track, tracks)}
              >
                {track.name}
              </button>
              <span className='track-artist'>{track.artist}</span>
              {reason && (
                <span className='track-reason'>{reason}</span>
              )}
              {!track.mediaurl && (
                <button
                  type='button'
                  className='btn btn-quiet btn-sm track-attach'
                  onClick={() => onAttach(track)}
                >
                  Link file
                </button>
              )}
            </div>

            <div className='track-album'>
              {showAlbum && (
                <>
                  {albumFor ? (
                    <Link to={`/album/${encodeURIComponent(albumFor(track))}`}>{track.album}</Link>
                  ) : (
                    track.album
                  )}
                </>
              )}
            </div>

            <div className='track-status'>
              {track.hasLyrics && (
                <span
                  className='track-lyrics'
                  title={track.hasSyncedLyrics ? 'Synced lyrics available' : 'Lyrics available'}
                >
                  <span className='material-symbols-outlined' aria-hidden='true'>
                    lyrics
                  </span>
                </span>
              )}
            </div>

            <span className='track-time'>{track.duration}</span>
          </div>
        );
      })}
    </div>
  );
};

export default TrackList;
