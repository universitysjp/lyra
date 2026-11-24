/**
 * Maps catalog `localTrackId` values to object URLs of audio that stayed on this device.
 * Lyra never uploads audio, so playback is only possible for files the user has picked here.
 * Files are persisted in IndexedDB so the mapping survives a page reload.
 */

import { loadPersistedLocalAudio, persistLocalAudio } from './localAudioDb';

type Listener = () => void;

const urls = new Map<string, string>();
const listeners = new Set<Listener>();

let version = 0;
let hydration: Promise<void> | null = null;

const notify = (): void => {
  version += 1;
  listeners.forEach((listener) => listener());
};

/** Monotonic counter that changes whenever a file is attached; use it for React snapshots. */
export const localAudioVersion = (): number => version;

const revoke = (objectUrl: string): void => {
  try {
    URL.revokeObjectURL(objectUrl);
  } catch {
    // Environments without object URL support (tests) can ignore this.
  }
};

/** Registers an already created object URL for a track, optionally persisting its file. */
export const registerLocalAudio = (
  localTrackId: string,
  objectUrl: string,
  file?: File | Blob,
): string => {
  const previous = urls.get(localTrackId);

  if (previous && previous !== objectUrl) {
    revoke(previous);
  }

  urls.set(localTrackId, objectUrl);
  notify();

  if (file) {
    void persistLocalAudio(localTrackId, file);
  }

  return objectUrl;
};

/** Registers a File directly, creating the object URL and persisting it for reloads. */
export const attachLocalAudio = (localTrackId: string, file: File | Blob): string =>
  registerLocalAudio(localTrackId, URL.createObjectURL(file), file);

export const localAudioUrl = (localTrackId: string): string => urls.get(localTrackId) ?? '';

export const hasLocalAudio = (localTrackId: string): boolean => urls.has(localTrackId);

export const localAudioCount = (): number => urls.size;

export const subscribeLocalAudio = (listener: Listener): (() => void) => {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
};

/**
 * Rebuilds object URLs from IndexedDB. Safe to call more than once; only the first call
 * performs work. Resolves immediately when IndexedDB is unavailable (tests/SSR).
 */
export const hydrateLocalAudio = (): Promise<void> => {
  hydration ??= (async () => {
    const stored = await loadPersistedLocalAudio();

    if (stored.length === 0) {
      return;
    }

    stored.forEach(({ localTrackId, file }) => {
      if (!urls.has(localTrackId)) {
        urls.set(localTrackId, URL.createObjectURL(file));
      }
    });

    notify();
  })();

  return hydration;
};

/** Test helper: forgets every registration and revokes its URL. */
export const clearLocalAudio = (): void => {
  urls.forEach((url) => revoke(url));
  urls.clear();
  notify();
};
