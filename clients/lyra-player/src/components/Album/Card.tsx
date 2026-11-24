import { Link } from 'react-router-dom';

import Artwork from '../Artwork/Artwork';

import type { IAlbumGroup } from '../../types/types';

interface IProps {
  album: IAlbumGroup;
}

/** Album tile in the library grid. */
const Card: React.FC<IProps> = ({ album }) => (
  <Link to={`/album/${encodeURIComponent(album.id)}`} className='album-card'>
    <div className='album-card-art'>
      <Artwork
        className='album-card-img'
        src={album.image}
        seed={`${album.artist}::${album.name}`}
        initials={album.name}
        alt={`${album.name} cover`}
      />
      <span className='album-card-play' aria-hidden='true'>
        <span className='material-symbols-outlined'>play_arrow</span>
      </span>
    </div>
    <div className='album-card-info'>
      <strong>{album.name}</strong>
      <span>{album.artist}</span>
      <span>
        {album.songs} {album.songs === 1 ? 'track' : 'tracks'} ·{' '}
        {album.minutes === 0 ? 'under a minute' : `${album.minutes} min`}
      </span>
    </div>
  </Link>
);

export default Card;
