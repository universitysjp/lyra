import { Link } from 'react-router-dom';

import Banner from '../components/Feedback/Banner';
import MissingMediaNotice from '../components/Media/MissingMediaNotice';
import Paging from '../components/Paging/Paging';
import TrackList from '../components/TrackList/TrackList';

import useAttachAudio from '../hooks/useAttachAudio';
import useConnectLocalFiles from '../hooks/useConnectLocalFiles';
import useLibrary from '../hooks/useLibrary';

import { albumGroupId } from '../utils/artwork';
import { totalSeconds } from '../api/mappers';

/** The listener's own library: everything they have imported on this device. */
const LibraryPage: React.FC = () => {
  const { tracks, loading, error, page, pageSize, totalCount, totalPages, goToPage, reload } =
    useLibrary();
  const attach = useAttachAudio();
  const { connect, busy: connecting, result: connectResult } = useConnectLocalFiles();

  const minutes = Math.round(totalSeconds(tracks) / 60);
  const durationLabel = minutes === 0 ? 'under a minute' : `${minutes} min`;
  const isEmpty = !loading && totalCount === 0;

  return (
    <div className='page library-page no-select'>
      <header className='page-head'>
        <div>
          <h1 className='page-title'>Your library</h1>
          <p className='page-sub'>
            {totalCount === 0
              ? 'The music already on this device, in one place.'
              : `${totalCount} ${totalCount === 1 ? 'track' : 'tracks'} · ${durationLabel} on this page`}
          </p>
        </div>

        <div className='page-actions'>
          <button
            type='button'
            className='btn btn-ghost'
            onClick={reload}
            disabled={loading}
            title='Refresh your library'
          >
            <span className='material-symbols-outlined' aria-hidden='true'>
              refresh
            </span>
            Refresh
          </button>
          <button
            type='button'
            className='btn btn-ghost'
            onClick={() => void connect()}
            disabled={connecting}
            title='Reconnect audio files already imported'
          >
            <span className='material-symbols-outlined' aria-hidden='true'>
              folder_open
            </span>
            {connecting ? 'Connecting…' : 'Connect files'}
          </button>
          <Link to='/import' className='btn btn-primary'>
            <span className='material-symbols-outlined' aria-hidden='true'>
              add
            </span>
            Add music
          </Link>
        </div>
      </header>

      {connectResult && (
        <p className='notice'>
          {connectResult.matched} of {connectResult.total} files reconnected.
          {connectResult.matched === 0
            ? ' No matching tracks were found. Add the files again to import them.'
            : ' They will still play after a reload.'}
        </p>
      )}

      <MissingMediaNotice />
      <Banner message={error} onRetry={reload} />

      {loading ? (
        <div className='skeleton-list' aria-label='Loading your library'>
          {[0, 1, 2, 3].map((row) => (
            <div key={row} className='skeleton-row' />
          ))}
        </div>
      ) : isEmpty ? (
        <div className='empty-state'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            library_music
          </span>
          <h2>Your library is quiet</h2>
          <p>
            Bring in the music already saved on this device. Lyra reads the tags, keeps the audio
            here, and makes the collection easy to browse and play.
          </p>
          <div className='empty-state-actions'>
            <Link to='/import' className='btn btn-primary'>
              <span className='material-symbols-outlined' aria-hidden='true'>
                add
              </span>
              Add music
            </Link>
            <button type='button' className='btn btn-ghost' onClick={() => void connect()}>
              <span className='material-symbols-outlined' aria-hidden='true'>
                folder_open
              </span>
              Connect existing files
            </button>
          </div>
        </div>
      ) : (
        <TrackList
          tracks={tracks}
          onAttach={(track) => void attach(track, tracks)}
          albumFor={(track) => albumGroupId(track.artist, track.album)}
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
  );
};

export default LibraryPage;
