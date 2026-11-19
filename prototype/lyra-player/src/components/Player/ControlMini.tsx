// hooks
import useTrack from '../../hooks/useTrack';

const ControlMini: React.FC = () => {
  const { currentState, currentTrack, queue, toggleTrack } = useTrack();

  if (currentTrack) {
    return (
      <button
        type='button'
        className='play-button active-opacity'
        title={currentState === 'playing' ? 'Pause' : 'Play'}
        onClick={() => toggleTrack(currentTrack, queue ?? [])}
      >
        <span className='material-symbols-outlined'>
          {currentState === 'playing' ? 'pause' : 'play_arrow'}
        </span>
      </button>
    );
  }

  return null;
};

export default ControlMini;
