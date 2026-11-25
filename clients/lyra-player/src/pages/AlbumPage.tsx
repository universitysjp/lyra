import { useEffect, useRef, useState } from 'react';

import { Link, useParams } from 'react-router-dom';

import Artwork from '../components/Artwork/Artwork';
import Banner from '../components/Feedback/Banner';
import MissingMediaNotice from '../components/Media/MissingMediaNotice';
import TrackList from '../components/TrackList/TrackList';

import useAttachAudio from '../hooks/useAttachAudio';
import useLibrary from '../hooks/useLibrary';
import useTrack from '../hooks/useTrack';

import { describeError } from '../api/errors';

import type { IAlbumGroup, ITrack } from '../types/types';

/** One album: artwork, actions and its tracks. */
const AlbumPage: React.FC = () => {
  const { id } = useParams();
  const { albums, loadAlbums } = useLibrary();
  const { currentTrack, currentState, playTrack, toggleTrack } = useTrack();
  const attach = useAttachAudio();

  const [loaded, setLoaded] = useState<IAlbumGroup[]>(albums);
  const [error, setError] = useState<string | null>(null);
  const widened = useRef<boolean>(false);

  useEffect(() => {
    setLoaded(albums);
  }, [albums]);

  useEffect(() => {
    if (widened.current) {
      return;
    }

    widened.current = true;

    void loadAlbums()
      .then(setLoaded)
      .catch((caught: unknown) => setError(describeError(caught)));
  }, [loadAlbums]);

  const album = loaded.find((item) => item.id === decodeURIComponent(id ?? ''));

  if (!album) {
    return (
      <div className='page'>
        <div className='empty-state'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            album
          </span>
          <h2>Album not found</h2>
          <p>This record is not part of your library anymore.</p>
          <div className='empty-state-actions'>
            <Link to='/albums' className='btn btn-primary'>
              Back to albums
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const hasCurrentTrack = album.tracks.some((track) => track.id === currentTrack?.id);
  const isPlaying = hasCurrentTrack && currentState === 'playing';

  const play = (): void => {
    if (album.tracks.length === 0) {
      return;
    }

    if (hasCurrentTrack && currentTrack) {
      toggleTrack(currentTrack, album.tracks);

      return;
    }

    const start = album.tracks.find((track: ITrack) => Boolean(track.mediaurl)) ?? album.tracks[0];

    if (start) {
      playTrack(start, album.tracks);
    }
  };

  const shuffle = (): void => {
    const playable = album.tracks.filter((track) => Boolean(track.mediaurl));
    const pool = playable.length > 0 ? playable : album.tracks;
    const random = pool[Math.floor(Math.random() * pool.length)];

    if (random) {
      playTrack(random, album.tracks);
    }
  };

  return (
    <div className='page album-page no-select'>
      <div>
        <Link to='/albums' className='btn btn-quiet btn-sm'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            arrow_back
          </span>
          Albums
        </Link>
      </div>

      <Banner message={error} />
      <MissingMediaNotice />

      <header className='album-hero'>
        <Artwork
          className='album-cover'
          src={album.image}
          seed={`${album.artist}::${album.name}`}
          initials={album.name}
          alt={`${album.name} cover`}
        />

        <div className='album-hero-info'>
          <span className='album-kicker'>Album</span>
          <h1 className='album-hero-title'>{album.name}</h1>
          <div className='album-meta'>
            <Link to='/albums'>{album.artist}</Link>
            <span className='dot' aria-hidden='true' />
            <span>
              {album.songs} {album.songs === 1 ? 'track' : 'tracks'}
            </span>
            <span className='dot' aria-hidden='true' />
            <span>{album.minutes === 0 ? 'under a minute' : `${album.minutes} min`}</span>
          </div>

          <div className='album-actions'>
            <button
              type='button'
              className='btn btn-primary'
              onClick={play}
              disabled={album.tracks.length === 0}
            >
              <span className='material-symbols-outlined' aria-hidden='true'>
                {isPlaying ? 'pause' : 'play_arrow'}
              </span>
              {isPlaying ? 'Pause' : 'Play'}
            </button>
            <button
              type='button'
              className='btn btn-ghost'
              onClick={shuffle}
              disabled={album.tracks.length === 0}
            >
              <span className='material-symbols-outlined' aria-hidden='true'>
                shuffle
              </span>
              Shuffle
            </button>
          </div>
        </div>
      </header>

      <TrackList
        tracks={album.tracks}
        showAlbum={false}
        onAttach={(track) => void attach(track, album.tracks)}
      />
    </div>
  );
};

export default AlbumPage;
