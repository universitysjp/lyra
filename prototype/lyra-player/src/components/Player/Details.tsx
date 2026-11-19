// hooks
import useTrack from '../../hooks/useTrack';

const Details: React.FC = () => {
  const { currentTrack } = useTrack();

  if (currentTrack) {
    return (
      <div className='player-information flex flex-gap flex-grow flex-h-center flex-v-center'>
        <div className='mini-image' style={{ backgroundImage: `url(${currentTrack.artwork})` }} />
        <div className='track-info flex flex-column'>
          <div className='flex flex-gap-small flex-v-center'>
            <strong>{currentTrack.name}</strong>
          </div>
          <span className='flex flex-gap-small'>
            <em>{currentTrack.artist}</em>
            &bull;
            <em>{currentTrack.album}</em>
          </span>
        </div>
      </div>
    );
  }

  return null;
};

export default Details;
