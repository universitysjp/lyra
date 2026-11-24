import { useCallback, useState } from 'react';

import Banner from '../Feedback/Banner';

import useLibrary from '../../hooks/useLibrary';

import { isAudioFile, pickAudioFiles } from '../../utils/filePicker';
import { registerLocalAudio } from '../../utils/localAudioStore';
import { readLocalTrack } from '../../utils/localFiles';
import { toMetadataItems } from '../../api/mappers';
import { formatDuration } from '../../utils/toolbox';

import type { LocalTrack } from '../../utils/localFiles';

/**
 * Reads tags from local audio files and adds them to the library. The files themselves
 * stay in the browser, and are persisted so the tracks remain playable after a reload.
 */
const ImportPanel: React.FC = () => {
  const { importTracks, importing, lastImport, error } = useLibrary();

  const [pending, setPending] = useState<LocalTrack[]>([]);
  const [reading, setReading] = useState<boolean>(false);
  const [localError, setLocalError] = useState<string | null>(null);
  const [dragging, setDragging] = useState<boolean>(false);

  const addFiles = useCallback(async (files: File[]): Promise<void> => {
    const audio = files.filter(isAudioFile);

    if (audio.length === 0) {
      setLocalError('No audio files found. Choose MP3, M4A, FLAC, OGG or WAV files.');

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
          upload_file
        </span>
        <h3>Drop your music here</h3>
        <p>
          Add MP3, M4A, FLAC, OGG or WAV files. Tags are read locally, and the audio never leaves
          this device.
        </p>
        <div className='flex flex-gap-small flex-v-center'>
          <button
            type='button'
            className='btn btn-primary'
            onClick={() => void pickAudioFiles(true).then(addFiles)}
          >
            <span className='material-symbols-outlined' aria-hidden='true'>
              folder_open
            </span>
            Choose files
          </button>
          <span className='muted small'>
            {pending.length} {pending.length === 1 ? 'file' : 'files'} ready
            {reading ? ' · reading tags' : ''}
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
                  <button
                    type='button'
                    className='icon-btn'
                    title={`Remove ${track.title}`}
                    aria-label={`Remove ${track.title}`}
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

      <div className='import-actions'>
        <button
          type='button'
          className='btn btn-primary'
          disabled={pending.length === 0 || importing || reading}
          onClick={() => void sync()}
        >
          <span className='material-symbols-outlined' aria-hidden='true'>
            library_add
          </span>
          {importing ? 'Adding…' : `Add ${pending.length || ''} to library`.replace('  ', ' ')}
        </button>
        <button
          type='button'
          className='btn btn-ghost'
          disabled={pending.length === 0 || importing}
          onClick={() => setPending([])}
        >
          Clear
        </button>
      </div>

      {pending.length === 0 && !lastImport && (
        <p className='muted small'>Choose files to see them here before adding them.</p>
      )}

      {lastImport && (
        <p className='result'>
          Added <strong>{lastImport.imported}</strong>{' '}
          {lastImport.imported === 1 ? 'track' : 'tracks'}.
          {lastImport.duplicates > 0 && (
            <>
              {' '}
              {lastImport.duplicates} already in your library.
            </>
          )}
        </p>
      )}
    </section>
  );
};

export default ImportPanel;
