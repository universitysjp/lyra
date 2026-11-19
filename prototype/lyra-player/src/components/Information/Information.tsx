// components
import Image from './Image';
import Details from './Details';
import Buttons from './Buttons';
import Playlist from './Playlist';

// types
import type { IAlbumGroup, ITrack } from '../../types/types';

// interfaces
interface IProps {
  album: IAlbumGroup;
  onAttach: (track: ITrack) => void;
}

/** Header block of an album page: artwork, summary, actions and the track list. */
const Information: React.FC<IProps> = ({ album, onAttach }) => (
  <div className='information flex flex-column flex-gap no-select'>
    <Image album={album} />
    <Details album={album} />
    <Buttons album={album} />
    <Playlist album={album} onAttach={onAttach} />
  </div>
);

export default Information;
