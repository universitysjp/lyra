import { useEffect, useRef, useState } from 'react';

import { Link, useParams } from 'react-router-dom';

// components
import Cover from '../components/Cover/Cover';
import Banner from '../components/Feedback/Banner';
import Information from '../components/Information/Information';
import MissingMediaNotice from '../components/Media/MissingMediaNotice';

// hooks
import useAttachAudio from '../hooks/useAttachAudio';
import useLibrary from '../hooks/useLibrary';

// utils
import { describeError } from '../api/errors';

import type { IAlbumGroup } from '../types/types';

/** Detail view of one album group, using the template's cover + information layout. */
const AlbumPage: React.FC = () => {
  const { id } = useParams();
  const { albums, loadAlbums } = useLibrary();
  const attach = useAttachAudio();

  const [loaded, setLoaded] = useState<IAlbumGroup[]>(albums);
  const [error, setError] = useState<string | null>(null);
  const widened = useRef<boolean>(false);

  // The provider already groups the current page; widen to the max page size once.
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
      <div className='container flex flex-column flex-gap'>
        <p className='empty'>Album not found in this library page.</p>
        <Link to='/albums' className='button black'>
          Back to albums
        </Link>
      </div>
    );
  }

  return (
    <div className='album-page flex flex-column flex-gap no-select'>
      <Cover image={album.image} />

      <div className='container flex flex-column flex-gap'>
        <Banner message={error} />
        <MissingMediaNotice />
        <Information album={album} onAttach={(track) => void attach(track, album.tracks)} />
      </div>
    </div>
  );
};

export default AlbumPage;
