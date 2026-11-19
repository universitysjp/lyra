// hooks
import useTrack from '../../hooks/useTrack';

// components
import TrackLine from './TrackLine';

// types
import type { IAlbumGroup, ITrack } from '../../types/types';

// interfaces
interface IProps {
  album: IAlbumGroup;
  onAttach: (track: ITrack) => void;
}

/** Track list of an album page, driven by the shared playback context. */
const Playlist: React.FC<IProps> = ({ album, onAttach }) => {
  const { currentTrack, currentState, toggleTrack } = useTrack();

  return (
    <section className='playlist'>
      {album.tracks.map((item: ITrack) => (
        <TrackLine
          track={item}
          key={item.id}
          selected={currentTrack?.id === item.id}
          playing={currentTrack?.id === item.id && currentState === 'playing'}
          onPlay={() => toggleTrack(item, album.tracks)}
          onAttach={onAttach}
        />
      ))}
    </section>
  );
};

export default Playlist;
