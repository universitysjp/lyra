import { findId3Header, parseId3v2, tagsFromFileName, type Id3Tags } from './id3';

/** A track file the user picked on this device: metadata plus a local object URL. */
export interface LocalTrack {
  localTrackId: string;
  fileName: string;
  title: string;
  artist: string;
  album: string | null;
  trackNumber: number | null;
  durationSeconds: number;
  /** Where the title/artist came from, surfaced in the import preview. */
  source: 'id3' | 'filename';
  objectUrl: string;
  file: File;
}

export interface ReadLocalTrackOptions {
  /** Injected for tests; defaults to an <audio> metadata probe. */
  probeDuration?: (url: string) => Promise<number>;
  createObjectUrl?: (file: File) => string;
  readHeader?: (file: File) => Promise<Uint8Array>;
}

const DEFAULT_SCAN_BYTES = 1024 * 1024;

const readHeaderBytes = async (file: File, maxScanBytes: number): Promise<Uint8Array> => {
  const slice = file.slice(0, Math.min(file.size, maxScanBytes));

  return new Uint8Array(await slice.arrayBuffer());
};

/** Stable per-device id for an audio file; survives reloads so local playback stays matched. */
export const toLocalTrackId = (fileName: string, size: number, lastModified: number): string =>
  `${fileName.replace(/\.[^./\\]+$/, '')}-${size}-${lastModified || 0}`;

/** Reads the real duration with an <audio> element, the browser's own decoder. */
export const probeDuration = (url: string, timeoutMs = 8000): Promise<number> =>
  new Promise((resolve) => {
    const audio = document.createElement('audio');
    let settled = false;

    const finish = (value: number): void => {
      if (settled) {
        return;
      }

      settled = true;
      audio.removeAttribute('src');
      resolve(Number.isFinite(value) && value > 0 ? Math.round(value) : 0);
    };

    audio.preload = 'metadata';
    audio.onloadedmetadata = () => finish(audio.duration);
    audio.onerror = () => finish(0);
    audio.src = url;

    window.setTimeout(() => finish(audio.duration), timeoutMs);
  });

/**
 * Extracts ID3 metadata for a local audio file. The file itself never leaves the browser;
 * only the returned metadata is posted to the catalog API.
 */
export const readLocalTrack = async (
  file: File,
  options: ReadLocalTrackOptions = {},
): Promise<LocalTrack> => {
  const {
    probeDuration: probe = probeDuration,
    createObjectUrl = (target: File) => URL.createObjectURL(target),
    readHeader = (target: File) => readHeaderBytes(target, DEFAULT_SCAN_BYTES),
  } = options;

  const bytes = await readHeader(file);
  const start = findId3Header(bytes);
  const tags: Id3Tags = start >= 0 ? parseId3v2(bytes.subarray(start)) : {};
  const fallback = tagsFromFileName(file.name);
  const objectUrl = createObjectUrl(file);
  const durationSeconds = await probe(objectUrl);

  const fromTags = Boolean(tags.title || tags.artist);
  const title = (tags.title || fallback.title || file.name).trim();
  const artist = (tags.artist || fallback.artist || 'Unknown Artist').trim();

  return {
    localTrackId: toLocalTrackId(file.name, file.size, file.lastModified),
    fileName: file.name,
    title,
    artist,
    album: tags.album?.trim() || null,
    trackNumber: tags.trackNumber ?? null,
    durationSeconds,
    source: fromTags ? 'id3' : 'filename',
    objectUrl,
    file,
  };
};

/** Guards against sending incomplete metadata the backend would reject wholesale. */
export const isImportable = (track: LocalTrack): boolean =>
  track.title.trim().length > 0 && track.artist.trim().length > 0 && track.durationSeconds > 0;
