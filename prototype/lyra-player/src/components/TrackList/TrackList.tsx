import { Link } from 'react-router-dom';

// hooks
import useTrack from '../../hooks/useTrack';

// types
import type { ITrack } from '../../types/types';

// interfaces
interface IProps {
  tracks: ITrack[];
  /** Rendered when a row has no local audio yet; usually opens the file picker. */
  onAttach: (track: ITrack) => void;
  /** Album lookup used for the artwork link target. */
  albumFor?: (track: ITrack) => string;
  emptyLabel?: string;
}

const TrackList: React.FC<IProps> = ({ tracks, onAttach, albumFor, emptyLabel = 'Nothing here yet.' }) => {
  const { currentTrack, currentState, queue, toggleTrack } = useTrack();

  if (tracks.length === 0) {
    return <p className='empty'>{emptyLabel}</p>;
  }

  return (
    <div className='rows no-select' role='list'>
      {tracks.map((track) => {
        const selected = currentTrack?.id === track.id;
        const playing = selected && currentState === 'playing';

        return (
          <div
            key={track.id}
            role='listitem'
            className={selected ? 'row active flex flex-gap flex-v-center' : 'row flex flex-gap flex-v-center'}
          >
            <button
              type='button'
              className='row-play active-opacity'
              title={playing ? `Pause ${track.name}` : `Play ${track.name}`}
              onClick={() => toggleTrack(track, queue && queue.length > 0 ? queue : tracks)}
            >
              <span className='material-symbols-outlined'>{playing ? 'pause' : 'play_arrow'}</span>
            </button>

            <div className='row-title flex flex-column'>
              <strong>{track.name}</strong>
              <span>{track.artist}</span>
            </div>

            <div className='row-album flex flex-column'>
              {albumFor ? (
                <Link to={`/album/${encodeURIComponent(albumFor(track))}`} className='underline'>
                  {track.album}
                </Link>
              ) : (
                <span>{track.album}</span>
              )}
            </div>

            <div className='row-badges flex flex-gap-small'>
              <span
                className={track.mediaurl ? 'chip chip-ok' : 'chip chip-muted'}
                title={
                  track.mediaurl
                    ? 'Audio file found on this device'
                    : 'Only metadata is in Lyra; attach the file to play it'
                }
              >
                {track.mediaurl ? 'on device' : 'metadata only'}
              </span>
              <span className={track.isEnriched ? 'chip chip-ok' : 'chip'}>
                {track.isEnriched ? 'enriched' : 'awaiting enrichment'}
              </span>
            </div>

            <span className='row-duration'>{track.duration}</span>

            {!track.mediaurl && (
              <button
                type='button'
                className='button black'
                onClick={() => onAttach(track)}
                title='Point Lyra at the local file for this track'
              >
                Attach file
              </button>
            )}
          </div>
        );
      })}
    </div>
  );
};

export default TrackList;
