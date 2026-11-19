// types
import type { IAlbumGroup } from '../../types/types';

// interfaces
interface IProps {
  album: IAlbumGroup;
}

/**
 * Album artwork. Lyra syncs metadata only, so the image is generated locally from the album
 * name; the flip side (hover) shows how much of the album is playable on this device.
 */
const Image: React.FC<IProps> = ({ album }) => {
  const playable = album.tracks.filter((track) => Boolean(track.mediaurl)).length;

  return (
    <section className='image'>
      <div className='image-inner'>
        <div className='front'>
          <img src={album.image} alt={album.name} draggable='false' />
        </div>
        <div className='back flex flex-h-center flex-v-end' title='Local availability'>
          <div className='back-info'>
            <h3>{album.tracks.length} tracks</h3>
            <p>
              {playable} of {album.tracks.length} can play from files on this device. Lyra only
              stores metadata, never the audio.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
};

export default Image;
