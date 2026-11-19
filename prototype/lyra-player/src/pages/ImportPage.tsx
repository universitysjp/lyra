import { useState } from 'react';

import { Link } from 'react-router-dom';

// components
import ImportPanel from '../components/Import/ImportPanel';

// data
import { SAMPLE_TRACKS } from '../data/sampleTracks';

// hooks
import useLibrary from '../hooks/useLibrary';

/**
 * Metadata ingestion page: extract tags locally, POST them in one batch, and see the
 * fingerprint dedupe by importing the same batch twice.
 */
const ImportPage: React.FC = () => {
  const { importTracks, importing, lastImport, error } = useLibrary();
  const [sampleError, setSampleError] = useState<string | null>(null);

  const syncSample = async (): Promise<void> => {
    setSampleError(null);

    const result = await importTracks(SAMPLE_TRACKS);

    if (!result) {
      setSampleError('The demo batch was rejected - check the banner above for the backend message.');
    }
  };

  return (
    <div className='albums flex flex-column flex-gap no-select'>
      <div className='container flex flex-column flex-gap'>
        <section className='flex flex-column flex-gap'>
          <div className='flex flex-space-between flex-v-center'>
            <h3>Sync local track metadata</h3>
            <Link to='/library' className='button black'>
              Back to library
            </Link>
          </div>

          <p className='muted'>
            Batches of up to 500 items go to <code>POST /api/catalog/tracks/batch</code>. The server
            fingerprints <em>title + artist + duration</em>, links only what is new for your account
            and publishes one <code>SongImportedEvent</code> per track through the transactional
            outbox.
          </p>

          {error && <p className='banner banner-error'>{error}</p>}
          {sampleError && <p className='banner banner-error'>{sampleError}</p>}

          <ImportPanel />

          <div className='flex flex-gap flex-v-center'>
            <button
              type='button'
              className='button black'
              disabled={importing}
              onClick={() => void syncSample()}
            >
              {importing ? 'Importing...' : `Load demo batch (${SAMPLE_TRACKS.length} tracks)`}
            </button>
            <span className='chip'>
              {lastImport
                ? `last import: ${lastImport.imported} new / ${lastImport.duplicates} duplicates`
                : 'no import yet'}
            </span>
          </div>

          <p className='muted small'>
            Run the import twice to watch dedupe: the second call reports every item as a duplicate
            and the library count stays the same.
          </p>
        </section>
      </div>
    </div>
  );
};

export default ImportPage;
