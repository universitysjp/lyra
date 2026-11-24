import { useEffect, useRef, useState } from 'react';

import { Link } from 'react-router-dom';

import Banner from '../components/Feedback/Banner';
import Card from '../components/Album/Card';

import useLibrary from '../hooks/useLibrary';

import { describeError } from '../api/errors';

import type { IAlbumGroup } from '../types/types';

/** Album grid grouped from the tracks in the library. */
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
    <div className='page albums-page no-select'>
      <header className='page-head'>
        <div>
          <h1 className='page-title'>Albums</h1>
          <p className='page-sub'>Every record already in your collection.</p>
        </div>
        <Link to='/library' className='btn btn-ghost'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            queue_music
          </span>
          All tracks
        </Link>
      </header>

      <Banner message={error} />

      {groups.length === 0 ? (
        <div className='empty-state'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            album
          </span>
          <h2>No albums yet</h2>
          <p>Add music with album tags and your records will appear here automatically.</p>
          <div className='empty-state-actions'>
            <Link to='/import' className='btn btn-primary'>
              Add music
            </Link>
          </div>
        </div>
      ) : (
        <div className='album-grid'>
          {groups.map((album) => (
            <Card key={album.id} album={album} />
          ))}
        </div>
      )}
    </div>
  );
};

export default AlbumsPage;
