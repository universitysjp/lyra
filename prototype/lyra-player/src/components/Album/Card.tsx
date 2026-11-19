import { Link } from 'react-router-dom';

// types
import type { IAlbumGroup } from '../../types/types';

// interfaces
interface IProps {
  album: IAlbumGroup;
}

/** Album tile for the grid, built from catalog metadata only. */
const Card: React.FC<IProps> = ({ album }) => (
  <Link to={`/album/${encodeURIComponent(album.id)}`} className='card active-opacity'>
    <div className='card-image' style={{ backgroundImage: `url(${album.image})` }} />
    <div className='card-info flex flex-column flex-gap-small'>
      <strong>{album.name}</strong>
      <span>{album.artist}</span>
      <span className='card-meta'>
        {album.songs} tracks - {album.minutes} min
      </span>
    </div>
  </Link>
);

export default Card;
