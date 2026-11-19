// hooks
import useTrack from '../../hooks/useTrack';

const Control: React.FC = () => {
  const { prevTrack, nextTrack, currentState, currentTrack, queue, playPrevious, playNext, toggleTrack } =
    useTrack();

  if (!currentTrack) {
    return null;
  }

  return (
    <>
      <button
        type='button'
        title={prevTrack ? `Previous song: ${prevTrack.name}` : 'No previous song'}
        className={prevTrack ? 'active-opacity' : 'disabled active-opacity'}
        disabled={!prevTrack}
        onClick={playPrevious}
      >
        <span className='material-symbols-outlined'>skip_previous</span>
      </button>
      <button
        type='button'
        className='big active-opacity'
        title={currentState === 'playing' ? 'Pause' : 'Play'}
        onClick={() => toggleTrack(currentTrack, queue ?? [])}
      >
        <span className='material-symbols-outlined'>
          {currentState === 'playing' ? 'pause' : 'play_arrow'}
        </span>
      </button>
      <button
        type='button'
        title={nextTrack ? `Next song: ${nextTrack.name}` : 'No next song'}
        className={nextTrack ? 'active-opacity' : 'disabled active-opacity'}
        disabled={!nextTrack}
        onClick={playNext}
      >
        <span className='material-symbols-outlined'>skip_next</span>
      </button>
    </>
  );
};

export default Control;
