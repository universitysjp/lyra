// hooks
import useTrack from '../../hooks/useTrack';

interface IProps {
  lyricsOpen: boolean;
  onToggleLyrics: () => void;
}

const Details: React.FC<IProps> = ({ lyricsOpen, onToggleLyrics }) => {
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
        <button
          type='button'
          className={lyricsOpen ? 'lyrics-toggle active' : 'lyrics-toggle'}
          onClick={onToggleLyrics}
          title={lyricsOpen ? 'Hide lyrics' : 'Show lyrics'}
          aria-label={lyricsOpen ? 'Hide lyrics' : 'Show lyrics'}
          aria-pressed={lyricsOpen}
        >
          <span className='material-symbols-outlined' aria-hidden='true'>
            lyrics
          </span>
        </button>
      </div>
    );
  }

  return null;
};

export default Details;
