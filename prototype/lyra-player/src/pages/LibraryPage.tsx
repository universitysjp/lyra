import { Link } from 'react-router-dom';

// components
import Banner from '../components/Feedback/Banner';
import Cover from '../components/Cover/Cover';
import Paging from '../components/Paging/Paging';
import TrackList from '../components/TrackList/TrackList';
import MissingMediaNotice from '../components/Media/MissingMediaNotice';

// hooks
import useAttachAudio from '../hooks/useAttachAudio';
import useLibrary from '../hooks/useLibrary';

// utils
import { albumGroupId, artworkFor } from '../utils/artwork';
import { totalSeconds } from '../api/mappers';

/**
 * The caller's catalog: GET /api/catalog/tracks, newest first, one backend page at a time.
 * Rows only play when the matching file has been attached on this device.
 */
const LibraryPage: React.FC = () => {
  const { tracks, loading, error, page, pageSize, totalCount, totalPages, goToPage, reload } =
    useLibrary();
  const attach = useAttachAudio();

  const minutes = Math.round(totalSeconds(tracks) / 60);

  return (
    <div className='library flex flex-column flex-gap no-select'>
      <Cover image={tracks[0]?.artwork ?? artworkFor('lyra library', 'Lyra')} />

      <div className='container flex flex-column flex-gap'>
        <section className='hero flex flex-column flex-gap-small'>
          <h1>Your library</h1>
          <em>
            {totalCount} synced track{totalCount === 1 ? '' : 's'} - {minutes} minutes on this page
          </em>
          <p className='muted'>
            Newest first, served through the gateway from PostgreSQL with a Redis cache-aside layer.
            Importing a batch invalidates the cached pages.
          </p>
        </section>

        <div className='flex flex-gap-small flex-v-center'>
          <button type='button' className='button black' onClick={reload} disabled={loading}>
            Refresh
          </button>
          <Link to='/import' className='button black'>
            Sync metadata
          </Link>
        </div>

        <MissingMediaNotice />
        <Banner message={error} onRetry={reload} />

        {loading ? (
          <p className='empty'>Loading your library...</p>
        ) : (
          <TrackList
            tracks={tracks}
            onAttach={(track) => void attach(track, tracks)}
            albumFor={(track) => albumGroupId(track.artist, track.album)}
            emptyLabel='No tracks yet. Sync local files or load the demo batch on the metadata page.'
          />
        )}

        <Paging
          page={page}
          totalPages={totalPages}
          totalCount={totalCount}
          pageSize={pageSize}
          loading={loading}
          onChange={goToPage}
        />
      </div>
    </div>
  );
};

export default LibraryPage;
