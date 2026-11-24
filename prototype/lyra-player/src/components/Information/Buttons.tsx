// hooks
import useLibrary from '../../hooks/useLibrary';
import useTrack from '../../hooks/useTrack';

// types
import type { IAlbumGroup, ITrack } from '../../types/types';

// interfaces
interface IProps {
  album: IAlbumGroup;
}

/** Album actions: play the first playable track, shuffle, or reload the library page. */
const Buttons: React.FC<IProps> = ({ album }) => {
  const { reload } = useLibrary();
  const { currentTrack, currentState, playTrack, toggleTrack } = useTrack();

  /** First track with local audio, falling back to the first track of the album. */
  const pickStartTrack = (): ITrack | undefined =>
    album.tracks.find((track) => Boolean(track.mediaurl)) ?? album.tracks[0];

  /**
   * Plays the album, resuming the current track when it already belongs to this album.
   */
  const onPlayPause = (): void => {
    if (album.tracks.length === 0) {
      return;
    }

    const sameAlbum = album.tracks.some((track) => track.id === currentTrack?.id);

    if (sameAlbum && currentTrack) {
      toggleTrack(currentTrack, album.tracks);

      return;
    }

    const start = pickStartTrack();

    if (start) {
      playTrack(start, album.tracks);
    }
  };

  /** Starts playback from a random track of the album. */
  const onShuffle = (): void => {
    if (album.tracks.length === 0) {
      return;
    }

    const random = album.tracks[Math.floor(Math.random() * album.tracks.length)];

    playTrack(random, album.tracks);
  };

  return (
    <section className='flex flex-gap flex-h-center flex-v-center'>
      <button
        type='button'
        className='flex flex-h-center flex-v-center small active-opacity'
        title='Shuffle this album'
        aria-label='Shuffle this album'
        disabled={album.tracks.length === 0}
        onClick={onShuffle}
      >
        <span className='material-symbols-outlined' aria-hidden='true'>
          shuffle
        </span>
      </button>
      <button
        type='button'
        onClick={onPlayPause}
        disabled={album.tracks.length === 0}
        className='play-button flex flex-h-center flex-v-center active-opacity'
        title='Play or pause this album'
        aria-label='Play or pause this album'
      >
        <span className='material-symbols-outlined' aria-hidden='true'>
          {currentState === 'playing' ? 'pause' : 'play_arrow'}
        </span>
      </button>
      <button
        type='button'
        className='flex flex-h-center flex-v-center small active-opacity'
        title='Reload this page of the library'
        aria-label='Reload this page of the library'
        onClick={reload}
      >
        <span className='material-symbols-outlined' aria-hidden='true'>
          refresh
        </span>
      </button>
    </section>
  );
};

export default Buttons;
