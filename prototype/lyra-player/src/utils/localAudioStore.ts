/**
 * Maps catalog `localTrackId` values to object URLs of audio that stayed on this device.
 * Lyra never uploads audio, so playback is only possible for files the user has picked here.
 */

type Listener = () => void;

const urls = new Map<string, string>();
const listeners = new Set<Listener>();

let version = 0;

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

/** Registers an already created object URL for a track. */
export const registerLocalAudio = (localTrackId: string, objectUrl: string): string => {
  const previous = urls.get(localTrackId);

  if (previous && previous !== objectUrl) {
    revoke(previous);
  }

  urls.set(localTrackId, objectUrl);
  notify();

  return objectUrl;
};

/** Registers a File directly, creating the object URL for it. */
export const attachLocalAudio = (localTrackId: string, file: File | Blob): string =>
  registerLocalAudio(localTrackId, URL.createObjectURL(file));

export const localAudioUrl = (localTrackId: string): string => urls.get(localTrackId) ?? '';

export const hasLocalAudio = (localTrackId: string): boolean => urls.has(localTrackId);

export const localAudioCount = (): number => urls.size;

export const subscribeLocalAudio = (listener: Listener): (() => void) => {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
};

/** Test helper: forgets every registration and revokes its URL. */
export const clearLocalAudio = (): void => {
  urls.forEach((url) => revoke(url));
  urls.clear();
  notify();
};
