import { useEffect, useRef, useState } from 'react';

import { Link } from 'react-router-dom';

// components
import Banner from '../components/Feedback/Banner';
import Card from '../components/Album/Card';
import Search from '../components/Search/Search';

// hooks
import useLibrary from '../hooks/useLibrary';

// utils
import { describeError } from '../api/errors';

import type { IAlbumGroup } from '../types/types';

/**
 * Album grid grouped from catalog metadata. Loads one page at the backend maximum of 200
 * tracks so grouping covers a large library without extra round-trips.
 */
const AlbumsPage: React.FC = () => {
  const { albums, loadAlbums } = useLibrary();

  const [groups, setGroups] = useState<IAlbumGroup[]>(albums);
  const [error, setError] = useState<string | null>(null);
  const started = useRef<boolean>(false);

  useEffect(() => {
    if (started.current) {
      return;
    }

    started.current = true;

    void loadAlbums()
      .then(setGroups)
      .catch((caught: unknown) => setError(describeError(caught)));
  }, [loadAlbums]);

  return (
    <div className='albums flex flex-column flex-gap no-select'>
      <div className='container flex flex-column flex-gap'>
        <Search />

        <section className='flex flex-column flex-gap'>
          <div className='flex flex-space-between flex-v-center'>
            <h3>Albums in your library</h3>
            <Link to='/library' className='button black'>
              All tracks
            </Link>
          </div>

          <Banner message={error} />

          {groups.length === 0 ? (
            <p className='empty'>
              Nothing grouped yet - albums appear after you sync metadata from local files.
            </p>
          ) : (
            <div className='grid flex-gap'>
              {groups.map((album) => (
                <Card key={album.id} album={album} />
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
};

export default AlbumsPage;
