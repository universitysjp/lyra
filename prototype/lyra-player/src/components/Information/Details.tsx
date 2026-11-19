import { Link } from 'react-router-dom';

// types
import type { IAlbumGroup } from '../../types/types';

// interfaces
interface IProps {
  album: IAlbumGroup;
}

/** Title block for an album, using catalog metadata only: artist, album, counts, length. */
const Details: React.FC<IProps> = ({ album }) => (
  <section className='album flex flex-column flex-gap-small flex-h-center flex-v-center'>
    <h1>
      <Link to='/albums' className='active-opacity underline'>
        {album.artist}
      </Link>
    </h1>
    <h2>{album.name}</h2>
    <div className='album-info flex flex-gap-small flex-v-center'>
      <em>
        {album.songs} Songs - {album.minutes} Minutes
      </em>
    </div>
  </section>
);

export default Details;
