// types
import type { ITrack } from '../../types/types';

// interfaces
interface IProps {
  track: ITrack;
  playing?: boolean;
  selected?: boolean;
  onPlay: () => void;
  onAttach: (track: ITrack) => void;
}

/** One row of an album page: play control, title, Lyra status chips, duration. */
const TrackLine: React.FC<IProps> = ({
  track,
  onPlay,
  onAttach,
  playing = false,
  selected = false,
}) => (
  <div
    className={
      selected
        ? 'active track flex flex-gap flex-v-center flex-space-between active-opacity'
        : 'track flex flex-gap flex-v-center flex-space-between active-opacity'
    }
  >
    <button type='button' onClick={onPlay} title={playing ? `Pause ${track.name}` : `Play ${track.name}`}>
      {selected && playing ? (
        <span className='flex flex-h-center flex-v-center track-number material-symbols-outlined'>
          pause
        </span>
      ) : (
        <>
          <span className='number flex flex-h-center flex-v-center track-number'>{track.no}</span>
          <span className='control flex flex-h-center flex-v-center track-number material-symbols-outlined'>
            play_arrow
          </span>
        </>
      )}
    </button>

    <div className='flex flex-column flex-grow'>
      <button type='button' className='name-button' onClick={onPlay}>
        {track.name}
      </button>
      <div className='flex flex-gap-small flex-v-center'>
        <span className={track.mediaurl ? 'chip chip-ok' : 'chip chip-muted'}>
          {track.mediaurl ? 'on device' : 'metadata only'}
        </span>
        <em>{track.isEnriched ? 'mood + lyrics ready' : 'awaiting enrichment'}</em>
      </div>
    </div>

    {!track.mediaurl && (
      <button type='button' className='button black' onClick={() => onAttach(track)}>
        Attach file
      </button>
    )}

    <span>{track.duration}</span>
  </div>
);

export default TrackLine;
