import { useCallback, useState } from 'react';

import Banner from '../Feedback/Banner';

// hooks
import useLibrary from '../../hooks/useLibrary';

// utils
import { isAudioFile, pickAudioFiles } from '../../utils/filePicker';
import { registerLocalAudio } from '../../utils/localAudioStore';
import { readLocalTrack } from '../../utils/localFiles';
import { toMetadataItems } from '../../api/mappers';
import { formatDuration } from '../../utils/toolbox';

import type { LocalTrack } from '../../utils/localFiles';

/**
 * Extracts ID3 metadata from files on this device and posts it to
 * POST /api/catalog/tracks/batch. The audio never leaves the browser.
 */
const ImportPanel: React.FC = () => {
  const { importTracks, importing, lastImport, error } = useLibrary();

  const [pending, setPending] = useState<LocalTrack[]>([]);
  const [reading, setReading] = useState<boolean>(false);
  const [localError, setLocalError] = useState<string | null>(null);
  const [dragging, setDragging] = useState<boolean>(false);

  /** Reads metadata for each dropped or picked file, keeping the list deduplicated by id. */
  const addFiles = useCallback(async (files: File[]): Promise<void> => {
    const audio = files.filter(isAudioFile);

    if (audio.length === 0) {
      setLocalError('No audio files found. Lyra understands MP3, M4A, FLAC, OGG and WAV tags.');

      return;
    }

    setReading(true);
    setLocalError(null);

    const read: LocalTrack[] = [];

    for (const file of audio) {
      try {
        read.push(await readLocalTrack(file));
      } catch (caught: unknown) {
        setLocalError(caught instanceof Error ? caught.message : 'Could not read that file.');
      }
    }

    setPending((current) => {
      const seen = new Set(current.map((item) => item.localTrackId));

      return [...current, ...read.filter((item) => !seen.has(item.localTrackId))];
    });
    setReading(false);
  }, []);

  const sync = useCallback(async (): Promise<void> => {
    const result = await importTracks(toMetadataItems(pending));

    if (!result) {
      return;
    }

    // Metadata is on the server now; reuse the probe URL and persist the file so
    // those rows stay playable after a reload.
    pending.forEach((track) =>
      registerLocalAudio(track.localTrackId, track.objectUrl, track.file),
    );
    setPending([]);
  }, [importTracks, pending]);

  return (
    <section className='import flex flex-column flex-gap'>
      <div
        className={dragging ? 'dropzone dropzone-active' : 'dropzone'}
        onDragOver={(event) => {
          event.preventDefault();
          setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(event) => {
          event.preventDefault();
          setDragging(false);
          void addFiles(Array.from(event.dataTransfer.files));
        }}
      >
        <span className='material-symbols-outlined' aria-hidden='true'>
          library_music
        </span>
        <p>
          Drop local audio files here. Tags are read in your browser with a small ID3 parser, only
          the metadata is sent to Lyra.
        </p>
        <div className='flex flex-gap-small flex-v-center'>
          <button
            type='button'
            className='button black'
            onClick={() => void pickAudioFiles(true).then(addFiles)}
          >
            Choose files
          </button>
          <span className='chip'>
            {pending.length} queued{reading ? ' - reading tags' : ''}
          </span>
        </div>
      </div>

      <Banner message={localError} onDismiss={() => setLocalError(null)} />
      <Banner message={error} />

      {pending.length > 0 && (
        <table className='preview'>
          <thead>
            <tr>
              <th scope='col'>Title</th>
              <th scope='col'>Artist</th>
              <th scope='col'>Album</th>
              <th scope='col'>Length</th>
              <th scope='col'>Source</th>
              <th scope='col'>
                <span className='sr-only'>Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {pending.map((track) => (
              <tr key={track.localTrackId}>
                <td>{track.title}</td>
                <td>{track.artist}</td>
                <td>{track.album ?? 'Single'}</td>
                <td>{formatDuration(track.durationSeconds)}</td>
                <td>
                  <span className={track.source === 'id3' ? 'chip chip-ok' : 'chip'}>
                    {track.source === 'id3' ? 'ID3 tag' : 'file name'}
                  </span>
                </td>
                <td>
                  <button
                    type='button'
                    className='icon-button'
                    title={`Remove ${track.title}`}
                    onClick={() =>
                      setPending((current) =>
                        current.filter((item) => item.localTrackId !== track.localTrackId),
                      )
                    }
                  >
                    <span className='material-symbols-outlined' aria-hidden='true'>
                      close
                    </span>
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div className='import-actions flex flex-gap flex-v-center'>
        <button
          type='button'
          className='button primary'
          disabled={pending.length === 0 || importing}
          onClick={() => void sync()}
          title='POST the queued metadata to /api/catalog/tracks/batch'
        >
          <span className='material-symbols-outlined' aria-hidden='true'>
            cloud_upload
          </span>
          <span>{importing ? 'Sending batch...' : `Send ${pending.length} track(s) to Lyra`}</span>
        </button>
        <button
          type='button'
          className='button black'
          disabled={pending.length === 0 || importing}
          onClick={() => setPending([])}
        >
          Clear queue
        </button>
        <span className='chip'>POST /api/catalog/tracks/batch</span>
      </div>

      {pending.length === 0 && (
        <p className='muted small'>
          The send button activates once at least one file is queued above.
        </p>
      )}

      {lastImport && (
        <p className='result'>
          Submitted <strong>{lastImport.submitted}</strong>, imported{' '}
          <strong>{lastImport.imported}</strong>, duplicates{' '}
          <strong>{lastImport.duplicates}</strong>. Re-importing the same files shows the
          fingerprint dedupe.
        </p>
      )}
    </section>
  );
};

export default ImportPanel;
