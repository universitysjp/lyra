/**
 * IndexedDB persistence for locally picked audio. The File objects never leave the
 * browser; IndexedDB lets the library keep playing after a page reload.
 */

const DB_NAME = 'lyra-local-audio';
const DB_VERSION = 1;
const STORE_NAME = 'files';

export interface StoredLocalAudio {
  localTrackId: string;
  file: Blob;
}

let databasePromise: Promise<IDBDatabase | null> | null = null;

const openDatabase = (): Promise<IDBDatabase | null> => {
  const factory = typeof window !== 'undefined' ? window.indexedDB : undefined;

  if (!factory) {
    return Promise.resolve(null);
  }

  return new Promise((resolve) => {
    const request = factory.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = () => {
      const database = request.result;

      if (!database.objectStoreNames.contains(STORE_NAME)) {
        database.createObjectStore(STORE_NAME, { keyPath: 'localTrackId' });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => resolve(null);
    request.onblocked = () => resolve(null);
  });
};

const getDatabase = (): Promise<IDBDatabase | null> => {
  databasePromise ??= openDatabase();

  return databasePromise;
};

const complete = (transaction: IDBTransaction): Promise<void> =>
  new Promise((resolve) => {
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => resolve();
    transaction.onabort = () => resolve();
  });

/** Stores one local file. Resolves silently when IndexedDB is unavailable. */
export const persistLocalAudio = async (
  localTrackId: string,
  file: Blob,
): Promise<void> => {
  const database = await getDatabase();

  if (!database) {
    return;
  }

  try {
    const transaction = database.transaction(STORE_NAME, 'readwrite');

    transaction.objectStore(STORE_NAME).put({ localTrackId, file });
    await complete(transaction);
  } catch {
    // Storage can be full or blocked (private mode); playback still works in memory.
  }
};

/** Loads every persisted file so the library can rebuild its object URLs. */
export const loadPersistedLocalAudio = async (): Promise<StoredLocalAudio[]> => {
  const database = await getDatabase();

  if (!database) {
    return [];
  }

  try {
    const transaction = database.transaction(STORE_NAME, 'readonly');
    const request = transaction.objectStore(STORE_NAME).getAll();
    const result = await new Promise<StoredLocalAudio[]>((resolve) => {
      request.onsuccess = () => resolve(request.result as StoredLocalAudio[]);
      request.onerror = () => resolve([]);
    });

    await complete(transaction);

    return result;
  } catch {
    return [];
  }
};

/** Removes one persisted file. */
export const removePersistedLocalAudio = async (localTrackId: string): Promise<void> => {
  const database = await getDatabase();

  if (!database) {
    return;
  }

  try {
    const transaction = database.transaction(STORE_NAME, 'readwrite');

    transaction.objectStore(STORE_NAME).delete(localTrackId);
    await complete(transaction);
  } catch {
    // Ignored: storage is best-effort.
  }
};

/** Test helper: drops every persisted file. */
export const clearPersistedLocalAudio = async (): Promise<void> => {
  const database = await getDatabase();

  if (!database) {
    return;
  }

  try {
    const transaction = database.transaction(STORE_NAME, 'readwrite');

    transaction.objectStore(STORE_NAME).clear();
    await complete(transaction);
  } catch {
    // Ignored: storage is best-effort.
  }
};
